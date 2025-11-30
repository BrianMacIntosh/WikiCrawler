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
