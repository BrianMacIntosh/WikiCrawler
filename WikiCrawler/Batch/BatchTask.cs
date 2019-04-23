using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
	private Thread m_heartbeatThread;

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
		EasyWeb.SetDelayForDomain(m_heartbeatEndpoint, 0f);

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
	
	protected void StartHeartbeat()
	{
		m_heartbeatThread = new Thread(HeartbeatThread);
		m_heartbeatThread.Start();
	}

	private void HeartbeatThread()
	{
		while (true)
		{
			SendHeartbeat(false);
			Thread.Sleep(HeartbeatInterval);
		}
	}

	protected void SendHeartbeat(bool terminate)
	{
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
		string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
		List<string> succeeded = m_succeeded.ToList();
		succeeded.Sort();
		File.WriteAllText(succeededFile, JsonConvert.SerializeObject(m_succeeded, Formatting.Indented));

		string failedFile = Path.Combine(ProjectDataDirectory, "failed.txt");
		File.WriteAllLines(failedFile, m_failMessages);
	}
}
