using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WikiCrawler
{
	class NsrwCleaner
	{
		public static void Do()
		{
			Console.WriteLine("Logging in...");
			MediaWiki.Api Api = new MediaWiki.Api(new Uri("https://commons.wikimedia.org/"));
			Api.AutoLogIn();

			//Read queue
			List<string> queue = new List<string>();
			using (StreamReader reader = new StreamReader(new FileStream("nsrwbad.txt", FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					queue.Add(reader.ReadLine());
				}
			}

			try
			{
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

					MediaWiki.Article article = Api.GetPage(file);
					string text = article.revisions.First().text;

					string extractedFrom = "{{extracted from|";
					if (text.Contains("{{LA2-NSRW}}")
						&& text.Contains("{{self|GFDL|cc-by-sa-3.0,2.5,2.0,1.0}}")
						&& text.Contains(extractedFrom))
					{
						//find source name
						int st = text.IndexOf(extractedFrom) + extractedFrom.Length;
						int d = st;
						for (; d < text.Length && text[d] != '}'; d++) ;
						string sourceFile = text.Substring(st, d - st);

						text = text.Replace("{{LA2-NSRW}}", "");
						text = text.Replace("{{self|GFDL|cc-by-sa-3.0,2.5,2.0,1.0}}", "{{ExtractedFromNSRW|" + sourceFile + "}}");
						text = text.Replace("{{own}}", "''[[s:en:The New Student's Reference Work|The New Student's Reference Work]]''");
						text = text.Replace(extractedFrom + sourceFile + "}}", "");

						if (text != article.revisions.First().text)
						{
							article.revisions.First().text = text;
							Api.SetPage(article, "extracted image", false, false, true);
						}

						queue.RemoveAt(c);
					}
				}
			}
			finally
			{
				//save progress
				using (StreamWriter writer = new StreamWriter(new FileStream("nsrwbad.txt", FileMode.Create)))
				{
					foreach (string file in queue)
					{
						writer.WriteLine(file);
					}
				}
			}
		}
	}
}
