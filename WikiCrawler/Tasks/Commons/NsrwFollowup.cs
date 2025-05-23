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

			Dictionary<PageTitle, List<PageTitle>> sourceMappedImages = new Dictionary<PageTitle, List<PageTitle>>();
			if (File.Exists("nsrwsource.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("nsrwsource.txt", FileMode.Open)))
				{
					while (!reader.EndOfStream)
					{
						PageTitle key = PageTitle.Parse(reader.ReadLine());
						int count = int.Parse(reader.ReadLine());
						List<PageTitle> destinations = new List<PageTitle>();
						for (int c = 0; c < count; c++)
						{
							destinations.Add(PageTitle.Parse(reader.ReadLine()));
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

					PageTitle file = PageTitle.SafeParse(queue[c]);
					file.Namespace = PageTitle.NS_File;

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
					PageTitle sourceFile = PageTitle.Parse(text.Substring(st, d - st));
					sourceFile.Namespace = PageTitle.NS_File;

					//Replacements
					text = text.Replace("{{Extracted from|", "{{ExtractedFromNSRW|");
					text = text.Replace("{{ExtractImage}}", "");
					text = text.Replace("{{LA2-NSRW}}", nsrwDerivedInfobox);

					if (WikiUtils.HasNoCategories(text))
					{
						text = text.TrimEnd() + "\n" + CommonsTemplates.MakeUncategorizedTemplate();
					}

					//Establish source
					if (!sourceMappedImages.ContainsKey(sourceFile))
						sourceMappedImages[sourceFile] = new List<PageTitle>();
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
				PageTitle[] keys = sourceMappedImages.Keys.ToArray();
				foreach (PageTitle s in keys)
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
					foreach (KeyValuePair<PageTitle, List<PageTitle>> kv in sourceMappedImages)
					{
						write.WriteLine(kv.Key);
						write.WriteLine(kv.Value.Count);
						foreach (PageTitle s in kv.Value)
						{
							write.WriteLine(s);
						}
					}
				}
			}
		}

		private static void UpdateSource(PageTitle sourceFile, List<PageTitle> destinationFiles)
		{
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
