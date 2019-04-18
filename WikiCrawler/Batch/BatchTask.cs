using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WikiCrawler;

public abstract class BatchTask
{
	protected string ImageCacheDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "images"); }
	}

	protected string MetadataCacheDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "data_cache"); }
	}

	protected string ProjectDataDirectory
	{
		get { return Path.Combine(Configuration.DataDirectory, m_projectKey); }
	}

	protected string GetImageCacheFilename(string key)
	{
		return Path.Combine(ImageCacheDirectory, key + ".jpg");
	}

	protected string GetImageCroppedFilename(string key)
	{
		return Path.Combine(ImageCacheDirectory, key + "_cropped.jpg");
	}

	protected string GetMetadataCacheFilename(string key)
	{
		return Path.Combine(MetadataCacheDirectory, key + ".json");
	}

	private Uri m_heartbeatEndpoint;
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60f);
	private DateTime m_nextHeartbeat;

	protected Dictionary<string, object> m_heartbeatData = new Dictionary<string, object>();

	protected HashSet<string> m_succeeded = new HashSet<string>();

	protected List<string> m_failMessages = new List<string>();

	protected string m_projectKey { get; private set; }
	protected ProjectConfig m_config { get; set; }

	public BatchTask(string key)
	{
		m_projectKey = key;
		m_config = JsonConvert.DeserializeObject<ProjectConfig>(
			File.ReadAllText(Path.Combine(ProjectDataDirectory, "config.json")));

		m_heartbeatEndpoint = new Uri(File.ReadAllText(Path.Combine(Configuration.DataDirectory, "heartbeat_endpoint.txt")));

		m_heartbeatData["taskKey"] = m_projectKey;
		m_heartbeatData["displayName"] = m_config.displayName;
		m_heartbeatData["nTotal"] = 0;
		m_heartbeatData["nCompleted"] = 0;
		m_heartbeatData["nDownloaded"] = 0;
		m_heartbeatData["nFailed"] = 0;
		m_heartbeatData["nFailedLicense"] = 0;
		m_heartbeatData["terminate"] = false;

		// load already-succeeded uploads
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
	
	protected void UpdateHeartbeat()
	{
		if (m_nextHeartbeat <= DateTime.Now)
		{
			SendHeartbeat(false);
			m_nextHeartbeat = DateTime.Now + HeartbeatInterval;
		}
	}

	protected void SendHeartbeat(bool terminate)
	{
		try
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(m_heartbeatEndpoint);
			m_heartbeatData["terminate"] = terminate;
			string serialized = JsonConvert.SerializeObject(m_heartbeatData);
			string dataString = "d=" + System.Web.HttpUtility.UrlEncode(serialized);
			EasyWeb.Post(request, dataString);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.ToString());
		}
	}

	protected virtual void SaveOut()
	{
		string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
		List<string> succeeded = m_succeeded.ToList();
		succeeded.Sort();
		File.WriteAllText(succeededFile, JsonConvert.SerializeObject(m_succeeded, Formatting.Indented));

		string failedFile = Path.Combine(ProjectDataDirectory, "failed.txt");
		File.WriteAllLines(failedFile, m_failMessages);
	}
}
