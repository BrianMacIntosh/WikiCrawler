using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Tasks;
using WikiCrawler;

public interface IBatchTask
{
	void Execute();

	string GetImageCacheDirectory();
}

/// <summary>
/// Base class for a batch file upload or download task.
/// </summary>
public abstract class BatchTask : BaseTask, IBatchTask
{
	public readonly string ProjectDataDirectory;

	public readonly string ImageCacheDirectory;
	public readonly string MetadataCacheDirectory;
	public readonly string MetadataTrashDirectory;

	public string GetImageCacheDirectory() { return ImageCacheDirectory; }

	/// <summary>
	/// Should this task persist its progress between runs?
	/// </summary>
	protected virtual bool GetPersistStatus() { return true; }

	protected List<string> m_failMessages = new List<string>();

	protected string m_projectKey { get; private set; }
	protected ProjectConfig m_config { get; set; }

	public BatchTask(string key)
	{
		HeartbeatEnabled = true;

		m_projectKey = key;
		ProjectDataDirectory = Path.Combine(Configuration.DataDirectory, m_projectKey);
		ImageCacheDirectory = Path.Combine(ProjectDataDirectory, "images");
		MetadataCacheDirectory = Path.Combine(ProjectDataDirectory, "data_cache");
		MetadataTrashDirectory = Path.Combine(ProjectDataDirectory, "data_trash");
		m_config = JsonConvert.DeserializeObject<ProjectConfig>(
			File.ReadAllText(Path.Combine(ProjectDataDirectory, "config.json")));

		m_heartbeatData["taskKey"] = m_projectKey;
		m_heartbeatData["nTotal"] = 0;
		m_heartbeatData["nCompleted"] = 0;
		m_heartbeatData["nDownloaded"] = 0;
		m_heartbeatData["nFailed"] = 0;
		m_heartbeatData["nFailedLicense"] = 0;
		m_heartbeatData["nDeclined"] = 0;
	}

	protected virtual void SaveOut()
	{
		string failedFile = Path.Combine(ProjectDataDirectory, "failed.txt");
		File.WriteAllLines(failedFile, m_failMessages);

		CreatorUtilityMeta.SaveOut();
	}
}
