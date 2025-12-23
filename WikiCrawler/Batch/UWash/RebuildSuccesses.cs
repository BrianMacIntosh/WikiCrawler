using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Task that rebuilds the succeeded list by seeing what's on Commons.
	/// </summary>
	public class RebuildSuccesses : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			ProjectConfig projectConfig = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			List<string> succeeded = HarvestUploadedKeys(projectConfig.masterCategory, projectConfig.filenameSuffix).ToList();
			succeeded.Sort();
			string succeededFile = Path.Combine(projectDir, "succeeded.json");
			File.WriteAllText(succeededFile, JsonConvert.SerializeObject(succeeded, Formatting.Indented));
		}

		public static string ExtractKeyFromTitle(PageTitle title, string projectSuffix)
		{
			string suffixStart = " (" + projectSuffix + " ";
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
