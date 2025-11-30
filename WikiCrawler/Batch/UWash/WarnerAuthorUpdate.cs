using MediaWiki;
using System;
using System.Collections.Generic;
using Tasks;
using UWash;

namespace WikiCrawler
{
	/// <summary>
	/// Fixup task for authors on the Warner task.
	/// </summary>
	public class WarnerAuthorUpdate : BatchTask
	{
		public WarnerAuthorUpdate()
			: base("warner")
		{
		}

		public override void Execute()
		{
			UWashUploader uploader = new UWashUploader("warner");

			foreach (Article article in GlobalAPIs.Commons.FetchArticles(BatchRebuildSuccesses.HarvestUploadedFiles(m_config.masterCategory, m_config.filenameSuffix)))
			{
				string keystr = BatchRebuildSuccesses.ExtractKeyFromTitle(article.title, m_config.filenameSuffix);
				int key = int.Parse(keystr);

				Dictionary<string, string> metadata = uploader.LoadMetadata(key);
				if (metadata == null)
				{
					ConsoleUtility.WriteLine(ConsoleColor.White, "[[:" + article.title.ToString() + "]]");
					ConsoleUtility.WriteLine(ConsoleColor.Red, ": <span style=\"color:red\">Missing metadata</span>");
					continue;
				}

				// what does the uploader want the author to be?
				UWashUploader.IntermediateData intermediate = new UWashUploader.IntermediateData(metadata, m_config);
				try
				{
					uploader.GetAuthor(key, metadata, "en", intermediate);
				}
				catch (FormatException e)
				{
					ConsoleUtility.WriteLine(ConsoleColor.White, "[[:" + article.title.ToString() + "]]");
					ConsoleUtility.WriteLine(ConsoleColor.Red, ": <span style=\"color:red\">" + e.Message + "</span>");
					continue;
				}
				string desiredAuthor = intermediate.Creator;

				// what is the author actually?
				CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);
				string actualAuthor = worksheet.Author;

				if (actualAuthor == desiredAuthor)
				{
					continue;
				}

				// replace automatically
				string autoReplaceCreator = "{{Creator:Arthur Churchill Warner}}";
				if (actualAuthor == autoReplaceCreator)
				{
					ConsoleUtility.WriteLine(ConsoleColor.White, "[[:" + article.title.ToString() + "]]");
					Console.WriteLine($": <nowiki>Automatically replacing '{autoReplaceCreator}' with '{desiredAuthor}'.</nowiki>");

					// replace photographer
					string textBefore = worksheet.Text.Substring(0, worksheet.AuthorIndex);
					string textAfter = worksheet.Text.Substring(worksheet.AuthorIndex + worksheet.Author.Length);
					worksheet.Text = textBefore + desiredAuthor + textAfter;

					// replace license
					string autoReplaceLicense1 = "{{PD-old-auto-1923|deathyear=1943}}";
					string autoReplaceLicense2 = "{{PD-old-auto-expired|deathyear=1943}}";
					string desiredLicense = uploader.GetLicenseTag(key, metadata, intermediate);
					if (worksheet.Text.Contains(autoReplaceLicense1))
					{
						worksheet.Text = worksheet.Text.Replace(autoReplaceLicense1, desiredLicense);
						Console.WriteLine($": <nowiki>Automatically replacing '{autoReplaceLicense1}' with '{desiredLicense}'.</nowiki>");
					}
					else if (worksheet.Text.Contains(autoReplaceLicense2))
					{
						worksheet.Text = worksheet.Text.Replace(autoReplaceLicense2, desiredLicense);
						Console.WriteLine($": <nowiki>Automatically replacing '{autoReplaceLicense2}' with '{desiredLicense}'.</nowiki>");
					}
					else
					{
						ConsoleUtility.WriteLine(ConsoleColor.Red, ": <span style=\"color:red\">Failed to find old license.</span>");
					}

					GlobalAPIs.Commons.SetPage(worksheet.Article, "Updating author ([[Commons:Batch uploading/University of Washington Digital Collections/Warner Author Repair]])");
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.White, "[[:" + article.title.ToString() + "]]");
					Console.WriteLine($": <b><span style=\"color:yellow\"><nowiki>Wants to replace '{actualAuthor}' with '{desiredAuthor}'.</nowiki></span></b>");
				}
			}

			uploader.SaveOut();
		}
	}
}
