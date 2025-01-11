using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using WikiCrawler;

public interface IBatchTask
{
	string GetImageCacheDirectory();
}

public abstract class BatchTask : IBatchTask
{
	public readonly string ProjectDataDirectory;

	public readonly string ImageCacheDirectory;
	public readonly string MetadataCacheDirectory;
	public readonly string MetadataTrashDirectory;

	public string GetImageCacheDirectory() { return ImageCacheDirectory; }

	public bool HeartbeatEnabled = true;

	/// <summary>
	/// Should this task persist its progress between runs?
	/// </summary>
	protected virtual bool GetPersistStatus() { return true; }

	private Uri m_heartbeatEndpoint;
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60f);
	private Thread m_heartbeatThread;

	protected Dictionary<string, object> m_heartbeatData = new Dictionary<string, object>();

	protected List<string> m_failMessages = new List<string>();

	protected string m_projectKey { get; private set; }
	protected ProjectConfig m_config { get; set; }

	public BatchTask(string key)
	{
		m_projectKey = key;
		ProjectDataDirectory = Path.Combine(Configuration.DataDirectory, m_projectKey);
		ImageCacheDirectory = Path.Combine(ProjectDataDirectory, "images");
		MetadataCacheDirectory = Path.Combine(ProjectDataDirectory, "data_cache");
		MetadataTrashDirectory = Path.Combine(ProjectDataDirectory, "data_trash");
		m_config = JsonConvert.DeserializeObject<ProjectConfig>(
			File.ReadAllText(Path.Combine(ProjectDataDirectory, "config.json")));

		m_heartbeatEndpoint = new Uri(File.ReadAllText(Path.Combine(Configuration.DataDirectory, "heartbeat_endpoint.txt")));
		EasyWeb.SetDelayForDomain(m_heartbeatEndpoint, 0f);

		m_heartbeatData["taskKey"] = m_projectKey;
		m_heartbeatData["displayName"] = m_config.displayName;
		m_heartbeatData["nTotal"] = 0;
		m_heartbeatData["nCompleted"] = 0;
		m_heartbeatData["nDownloaded"] = 0;
		m_heartbeatData["nFailed"] = 0;
		m_heartbeatData["nFailedLicense"] = 0;
		m_heartbeatData["nDeclined"] = 0;
		m_heartbeatData["terminate"] = false;
	}

	protected void StartHeartbeat()
	{
		if (HeartbeatEnabled)
		{
			m_heartbeatThread = new Thread(HeartbeatThread);
			m_heartbeatThread.Start();
		}
	}

	private void HeartbeatThread()
	{
		while (HeartbeatEnabled)
		{
			SendHeartbeat(false);
			Thread.Sleep(HeartbeatInterval);
		}
	}

	protected void SendHeartbeat(bool terminate)
	{
		if (!HeartbeatEnabled)
		{
			return;
		}
		if (terminate && m_heartbeatThread != null)
		{
			m_heartbeatThread.Abort();
			m_heartbeatThread = null;
		}
		try
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(m_heartbeatEndpoint);
			string serialized;
			lock (m_heartbeatData)
			{
				m_heartbeatData["terminate"] = terminate;
				serialized = JsonConvert.SerializeObject(m_heartbeatData);
			}
			string dataString = "d=" + System.Web.HttpUtility.UrlEncode(serialized);
			Stream response = EasyWeb.Post(request, dataString);
			response.Dispose();
		}
		catch (Exception e)
		{
			Console.WriteLine(e.ToString());
		}
	}

	protected virtual void SaveOut()
	{
		string failedFile = Path.Combine(ProjectDataDirectory, "failed.txt");
		File.WriteAllLines(failedFile, m_failMessages);

		if (CreatorUtilityMeta.IsInitialized)
		{
			CreatorUtility.SaveOut();
		}
	}
}
