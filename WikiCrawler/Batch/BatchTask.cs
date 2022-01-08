using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using WikiCrawler;

public abstract class BatchTask
{
	public readonly string ProjectDataDirectory;

	public readonly string ImageCacheDirectory;
	public readonly string MetadataCacheDirectory;
	public readonly string MetadataTrashDirectory;

	public virtual string GetImageCacheFilename(string key, Dictionary<string, string> metadata)
	{
		return Path.Combine(ImageCacheDirectory, key + ".jpg");
	}

	public virtual string GetImageCroppedFilename(string key, Dictionary<string, string> metadata)
	{
		return Path.Combine(ImageCacheDirectory, key + "_cropped.jpg");
	}

	public virtual string GetMetadataCacheFilename(string key)
	{
		return Path.Combine(MetadataCacheDirectory, key + ".json");
	}

	public virtual string GetMetadataTrashFilename(string key)
	{
		return Path.Combine(MetadataTrashDirectory, key + ".json");
	}

	public bool HeartbeatEnabled = true;

	/// <summary>
	/// Should this task remember which keys were finished and save them between runs?
	/// </summary>
	protected virtual bool GetSaveFinishedKeys() { return true; }

	private Uri m_heartbeatEndpoint;
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60f);
	private Thread m_heartbeatThread;

	protected Dictionary<string, object> m_heartbeatData = new Dictionary<string, object>();

	protected HashSet<string> m_succeeded = new HashSet<string>();

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

		// load already-succeeded uploads
		if (GetSaveFinishedKeys())
		{
			string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
			if (File.Exists(succeededFile))
			{
				string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(succeededFile, Encoding.UTF8));
				foreach (string suc in succeeded)
				{
					m_succeeded.Add(suc);
				}
			}
		}
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
		if (GetSaveFinishedKeys())
		{
			string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
			List<string> succeeded = m_succeeded.ToList();
			succeeded.Sort();
			File.WriteAllText(succeededFile, JsonConvert.SerializeObject(m_succeeded, Formatting.Indented));
		}

		string failedFile = Path.Combine(ProjectDataDirectory, "failed.txt");
		File.WriteAllLines(failedFile, m_failMessages);
	}
}
