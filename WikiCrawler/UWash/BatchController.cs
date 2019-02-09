using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WikiCrawler
{
	public static class BatchController
	{
		public static void DownloadAll()
		{
			foreach (string directory in Directory.GetDirectories(Configuration.DataDirectory))
			{
				string key = directory.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Last();
				Console.WriteLine(key);

				UWash.UWashDownloader downloader = new UWash.UWashDownloader(key);
				downloader.DownloadAll();
			}
		}

		public static void Download()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return;
			}

			UWash.UWashDownloader downloader = new UWash.UWashDownloader(projectKey);
			downloader.DownloadAll();
		}

		public static void Upload()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return;
			}

			UWash.UWashUploader uploader = new UWash.UWashUploader(projectKey);
			uploader.UploadAll();
		}

		public static void RebuildSuccesses()
		{
			Wikimedia.WikiApi commonsApi = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));

			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			ProjectConfig projectConfig = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			List<string> succeeded = new List<string>();

			string suffixStart = " (" + projectConfig.filenameSuffix + " ";
			foreach (Wikimedia.Article article in commonsApi.GetCategoryFiles(projectConfig.masterCategory))
			{
				Console.WriteLine(article.title);
				int tagIndex = article.title.IndexOf(suffixStart);
				if (tagIndex < 0)
				{
					continue;
				}
				int numStart = tagIndex + suffixStart.Length;
				int numEnd = article.title.IndexOf(')', numStart);
				string articleId = article.title.Substring(numStart, numEnd - numStart);
				succeeded.Add(articleId);
			}

			string succeededFile = Path.Combine(projectDir, "succeeded.json");
			File.WriteAllText(succeededFile, JsonConvert.SerializeObject(succeeded.ToArray()));
		}
	}
}

public class UWashException : Exception
{
	public UWashException(string message)
		: base(message)
	{

	}
}
