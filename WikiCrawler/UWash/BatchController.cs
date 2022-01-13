using MediaWiki;
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

		public static BatchDownloader CreateDownloader(string downloader, string projectKey)
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

			//TODO: don't deserialize this twice
			ProjectConfig config = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			BatchDownloader downloader = CreateDownloader(config.downloader, projectKey);
			downloader.DownloadAll();
		}

		public static BatchUploader CreateUploader(string uploader, string projectKey)
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

			//TODO: don't deserialize this twice
			ProjectConfig config = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			BatchUploader uploader = CreateUploader(config.uploader, projectKey);
			uploader.UploadAll();
		}

		public static void RebuildSuccesses()
		{
			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org/"));

			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			ProjectConfig projectConfig = JsonConvert.DeserializeObject<ProjectConfig>(
				File.ReadAllText(Path.Combine(projectDir, "config.json")));

			List<string> succeeded = new List<string>();

			string suffixStart = " (";// + projectConfig.filenameSuffix + " ";
			foreach (Article article in commonsApi.GetCategoryEntries(projectConfig.masterCategory, cmtype: CMType.file))
			{
				Console.WriteLine(article.title);
				int tagIndex = article.title.LastIndexOf(suffixStart);
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

		public static void RevalidateDownloads()
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

			BatchUploader uploader = CreateUploader(config.uploader, projectKey);
			foreach (string file in Directory.GetFiles(uploader.ImageCacheDirectory))
			{
				Console.WriteLine(Path.GetFileName(file));
				try
				{
					uploader.ValidateDownload(file);
				}
				catch (Exception e)
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
