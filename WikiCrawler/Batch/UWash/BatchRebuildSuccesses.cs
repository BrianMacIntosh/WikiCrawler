using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Task that rebuilds the succeeded list by seeing what's on Commons.
	/// </summary>
	public class BatchRebuildSuccesses : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			ProjectConfig projectConfig = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			List<string> succeeded = HarvestSucceeded(projectConfig.masterCategory);
			string succeededFile = Path.Combine(projectDir, "succeeded.json");
			File.WriteAllText(succeededFile, JsonConvert.SerializeObject(succeeded, Formatting.Indented));
		}

		private List<string> HarvestSucceeded(string masterCategory)
		{
			List<string> succeeded = new List<string>();

			string suffixStart = " (";// + projectConfig.filenameSuffix + " ";
			foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(PageTitle.Parse(masterCategory), cmtype: CMType.file))
			{
				Console.WriteLine(article.title);
				int tagIndex = article.title.Name.LastIndexOf(suffixStart);
				if (tagIndex < 0)
				{
					continue;
				}
				int numStart = tagIndex + suffixStart.Length;
				int numEnd = article.title.Name.IndexOf(')', numStart);
				string articleId = article.title.Name.Substring(numStart, numEnd - numStart);
				succeeded.Add(articleId);
			}

			succeeded.Sort();

			return succeeded;
		}
	}
}
