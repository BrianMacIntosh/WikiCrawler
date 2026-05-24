using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	public class NPGalleryRebuildSuccesses : RebuildSuccesses<Guid>
	{

	}

	/// <summary>
	/// Task that rebuilds the succeeded list by seeing what's on Commons.
	/// </summary>
	public class RebuildSuccesses<KeyType> : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			ProjectConfig config = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			// downloader is just for parsing keys
			BatchDownloader<KeyType> downloader = (BatchDownloader<KeyType>)BatchDown.CreateDownloader(config.downloader, projectKey);

			string statusFilePath = Path.Combine(projectDir, "status.json");
			Dictionary<KeyType, BatchItemStatus> itemStatus = JsonConvert.DeserializeObject<Dictionary<KeyType, BatchItemStatus>>(File.ReadAllText(statusFilePath));
			foreach (string succeededKey in HarvestUploadedKeys(config.masterCategory, config.filenameSuffix))
			{
				KeyType normalizedKey;
				try
				{
					normalizedKey = downloader.StringToKey(succeededKey);
				}
				catch (Exception ex)
				{
					continue;
				}

				if (!itemStatus.ContainsKey(normalizedKey))
				{
					Console.WriteLine($"Adding new key {normalizedKey}");
					itemStatus[normalizedKey] = BatchItemStatus.Succeeded;
				}
				else if (itemStatus[normalizedKey] != BatchItemStatus.Succeeded)
				{
					Console.WriteLine($"Updating key {normalizedKey}");
					itemStatus[normalizedKey] = BatchItemStatus.Succeeded;
				}
			}

			// save status file
			string statusFile = Path.Combine(projectDir, "status.json");
			File.WriteAllText(statusFile, JsonConvert.SerializeObject(itemStatus, Formatting.Indented));
		}

		public static string ExtractKeyFromTitle(PageTitle title, string projectSuffix)
		{
			string suffixStart = !string.IsNullOrEmpty(projectSuffix) ? $" ({projectSuffix} " : "(";
			int tagIndex = title.Name.LastIndexOf(suffixStart);
			if (tagIndex < 0)
			{
				return null;
			}
			int numStart = tagIndex + suffixStart.Length;
			int numEnd = title.Name.IndexOf(')', numStart);
			string articleId = title.Name.Substring(numStart, numEnd - numStart);
			return articleId;
		}

		/// <summary>
		/// Returns a list of all the keys of files that were batch-uploaded to the specified category.
		/// </summary>
		public static IEnumerable<string> HarvestUploadedKeys(string masterCategory, string projectSuffix)
		{
			foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(PageTitle.Parse(masterCategory), cmtype: CMType.file))
			{
				string key = ExtractKeyFromTitle(article.title, projectSuffix);
				if (key != null)
				{
					yield return key;
				}
			}
		}

		/// <summary>
		/// Returns a list of all the files that were batch-uploaded to the specified category.
		/// </summary>
		public static IEnumerable<PageTitle> HarvestUploadedFiles(string masterCategory, string projectSuffix)
		{
			foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(PageTitle.Parse(masterCategory), cmtype: CMType.file))
			{
				string key = ExtractKeyFromTitle(article.title, projectSuffix);
				if (key != null)
				{
					yield return article.title;
				}
			}
		}
	}
}
