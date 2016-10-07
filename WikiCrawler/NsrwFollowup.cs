﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace WikiCrawler
{
	class NsrwFollowup
	{
		private static Wikimedia.WikiApi Api;

		public static void Do()
		{
			//Read queue
			List<string> queue = new List<string>();
			using (StreamReader reader = new StreamReader(new FileStream("nsrwqueue.txt", FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					queue.Add(reader.ReadLine());
				}
			}

			string nsrwDerivedInfobox;
			using (StreamReader reader = new StreamReader(new FileStream("nsrwreplace.txt", FileMode.Open)))
			{
				nsrwDerivedInfobox = reader.ReadToEnd();
			}

			Dictionary<string, List<string>> sourceMappedImages = new Dictionary<string, List<string>>();
			if (File.Exists("nsrwsource.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("nsrwsource.txt", FileMode.Open)))
				{
					while (!reader.EndOfStream)
					{
						string key = reader.ReadLine();
						int count = int.Parse(reader.ReadLine());
						List<string> destinations = new List<string>();
						for (int c = 0; c < count; c++)
						{
							destinations.Add(reader.ReadLine());
						}
						sourceMappedImages[key] = destinations;
					}
				}
			}

			try
			{
				Console.WriteLine("Logging in...");
				Api = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));
				Api.LogIn();

				for (int c = queue.Count - 1; c >= 0; c--)
				{
					if (string.IsNullOrEmpty(queue[c].Trim()))
					{
						queue.RemoveAt(c);
						continue;
					}

					string file = queue[c];
					if (!file.StartsWith("File:"))
						file = "File:" + file;

					Console.WriteLine(file);

					Wikimedia.Article article = Api.GetPage(file);
					string text = article.revisions.First().text;

					//Find source article
					string extractedFrom = "{{Extracted from|";
					int extractedFromIndex = text.IndexOf(extractedFrom);

					if (extractedFromIndex < 0)
					{
						extractedFrom = "{{ExtractedFromNSRW|";
						extractedFromIndex = text.IndexOf(extractedFrom);
						if (extractedFromIndex < 0)
						{
							Console.WriteLine(file + ": no Extracted from template");
							continue;
						}
					}

					//Read it
					int st = extractedFromIndex + extractedFrom.Length;
					int d = st;
					for (; d < text.Length && text[d] != '}'; d++) ;
					string sourceFile = text.Substring(st, d - st);

					//Replacements
					text = text.Replace("{{Extracted from|", "{{ExtractedFromNSRW|");
					text = text.Replace("{{ExtractImage}}", "");
					text = text.Replace("{{LA2-NSRW}}", nsrwDerivedInfobox);

					if (Wikimedia.WikiUtils.HasNoCategories(text))
					{
						string month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
						text = text.TrimEnd() + "\n{{Uncategorized|year=" + DateTime.Now.Year + 
							"|month=" + month + "|day=" + DateTime.Now.Day + "}}";
					}

					//Establish source
					if (!sourceFile.StartsWith("File:"))
						sourceFile = "File:" + sourceFile;
					if (!sourceMappedImages.ContainsKey(sourceFile))
						sourceMappedImages[sourceFile] = new List<string>();
					sourceMappedImages[sourceFile].Add(file);

					text = text.Replace("\r\n", "\n");
					if (text != article.revisions.First().text)
					{
						article.revisions.First().text = text;
						Api.SetPage(article, "extracted image", false, false, true);
					}

					queue.RemoveAt(c);
				}

				//write out source files
				string[] keys = sourceMappedImages.Keys.ToArray();
				foreach (string s in keys)
				{
					UpdateSource(s, sourceMappedImages[s]);
					sourceMappedImages.Remove(s);
				}
			}
			finally
			{
				//save progress
				using (StreamWriter writer = new StreamWriter(new FileStream("nsrwqueue.txt", FileMode.Create)))
				{
					foreach (string file in queue)
					{
						writer.WriteLine(file);
					}
				}
				using (StreamWriter write = new StreamWriter(new FileStream("nsrwsource.txt", FileMode.Create)))
				{
					foreach (KeyValuePair<string, List<string>> kv in sourceMappedImages)
					{
						write.WriteLine(kv.Key);
						write.WriteLine(kv.Value.Count);
						foreach (string s in kv.Value)
						{
							write.WriteLine(s);
						}
					}
				}
			}
		}

		private static void UpdateSource(string sourceFile, List<string> destinationFiles)
		{
			sourceFile = sourceFile.Replace("Image:", "");

			Wikimedia.Article sourceArticle = Api.GetPage(sourceFile);

			string sourceText = sourceArticle.revisions.First().text;
			string files = string.Join("|", destinationFiles.ToArray());
			sourceText = sourceText.Replace("{{ExtractImage}}", "{{extracted image|" + files + "}}");

			if (sourceText != sourceArticle.revisions.First().text)
			{
				sourceArticle.revisions.First().text = sourceText;
				Api.SetPage(sourceArticle, "extracted image", false, false, true);
			}
		}
	}
}
