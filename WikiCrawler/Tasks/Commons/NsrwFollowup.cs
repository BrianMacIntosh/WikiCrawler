using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tasks.Commons
{
	/// <summary>
	/// Updates page text for images extracted from NSRW after extracting them with CropTool.
	/// </summary>
	public class NsrwFollowup : BaseTask
	{
		private static Api Api;

		public override void Execute()
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
				Api = GlobalAPIs.Commons;

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

					Article article = Api.GetPage(file);
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

					if (WikiUtils.HasNoCategories(text))
					{
						text = text.TrimEnd() + "\n" + CommonsTemplates.MakeUncategorizedTemplate();
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
						Api.EditPage(article, "extracted image", bot: false);
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

			Article sourceArticle = Api.GetPage(sourceFile);

			string sourceText = sourceArticle.revisions.First().text;
			string files = string.Join("|", destinationFiles.ToArray());
			sourceText = sourceText.Replace("{{ExtractImage}}", "{{extracted image|" + files + "}}");

			if (sourceText != sourceArticle.revisions.First().text)
			{
				sourceArticle.revisions.First().text = sourceText;
				Api.EditPage(sourceArticle, "extracted image", bot: false);
			}
		}
	}
}
