using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Task that finds the correct class for a batch download task and runs it.
	/// </summary>
	public class BatchDown : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return;
			}

			//TODO: don't deserialize this twice
			ProjectConfig config = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			IBatchDownloader downloader = CreateDownloader(config.downloader, projectKey);
			downloader.Execute();
		}

		public static IBatchDownloader CreateDownloader(string downloader, string projectKey)
		{
			switch (downloader)
			{
				case "UWash":
					return new UWash.UWashDownloader(projectKey);
				case "NPGallery":
					return new NPGallery.NPGalleryDownloader(projectKey);
				default:
					throw new NotImplementedException("Downloader '" + "'.");
			}
		}
	}

	/// <summary>
	/// Task that finds the correct class for a batch upload task and runs it.
	/// </summary>
	public class BatchUp : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return;
			}

			//TODO: don't deserialize this twice
			ProjectConfig config = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			IBatchUploader uploader = CreateUploader(config.uploader, projectKey);
			uploader.Execute();
		}

		public static IBatchUploader CreateUploader(string uploader, string projectKey)
		{
			switch (uploader)
			{
				case "UWash":
					return new UWash.UWashUploader(projectKey);
				case "NPGallery":
					return new NPGallery.NPGalleryUploader(projectKey);
				case "DSAL":
					return new Dsal.DsalUploader(projectKey);
				case "OEC":
					return new OEC.OecUploader(projectKey);
				case "GCBRoll":
					return new GCBRollUploader(projectKey);
				default:
					throw new NotImplementedException("Uploader '" + uploader + "'.");
			}
		}
	}

	/// <summary>
	/// Task that rebuilds the succeeded list by seeing what's on Commons.
	/// </summary>
	public class BatchRebuildSuccesses : BaseTask
	{
		public override void Execute()
		{
			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org/"));

			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			ProjectConfig projectConfig = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			List<string> succeeded = new List<string>();

			string suffixStart = " (";// + projectConfig.filenameSuffix + " ";
			foreach (Article article in commonsApi.GetCategoryEntries(PageTitle.Parse(projectConfig.masterCategory), cmtype: CMType.file))
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

			string succeededFile = Path.Combine(projectDir, "succeeded.json");
			succeeded.Sort();

			// clean up metadata for already-succeeded files
			foreach (string id in succeeded)
			{
				string metadataFile = Path.Combine(projectDir, "data_cache", id + ".json");
				if (File.Exists(metadataFile))
				{
					//TODO: create trash directory
					//TODO: get filenames from central source
					string targetFile = Path.Combine(projectDir, "data_trash", id + ".json");
					if (File.Exists(targetFile))
					{
						File.Delete(metadataFile);
					}
					else
					{
						File.Move(metadataFile, targetFile);
					}
				}
			}

			File.WriteAllText(succeededFile, JsonConvert.SerializeObject(succeeded, Formatting.Indented));
		}
	}

	/// <summary>
	/// Task that throws away downloaded data that's invalid.
	/// </summary>
	public class BatchRevalidateDownloads : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return;
			}

			//TODO: don't deserialize this twice
			ProjectConfig config = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			IBatchUploader uploader = BatchUp.CreateUploader(config.uploader, projectKey);
			foreach (string file in Directory.GetFiles(uploader.GetImageCacheDirectory()))
			{
				Console.WriteLine(Path.GetFileName(file));
				try
				{
					uploader.ValidateDownload(file);
				}
				catch (Exception)
				{
					File.Delete(file);
				}
			}
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
