﻿using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tasks.Commons
{
	public class NsrwCleaner : BaseTask
	{
		public override void Execute()
		{
			Api Api = GlobalAPIs.Commons;

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

					PageTitle file = PageTitle.Parse(queue[c]);
					file.Namespace = PageTitle.NS_File;

					Console.WriteLine(file);

					Article article = Api.GetPage(file);
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
							Api.EditPage(article, "extracted image", bot: false);
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
