using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using WikiCrawler;

namespace Tasks
{
	public class HeartbeatData
	{
		public HeartbeatData(string inTaskKey)
		{
			taskKey = inTaskKey;
		}

		public readonly string taskKey;
		public bool terminate = false;

		public int nEdits = 0;

		public int nTotal;
		public int nCompleted;
		public int nDownloaded;
		public int nFailed;
		public int nFailedLicense;
		public int nDeclined;
	}

	/// <summary>
	/// Base class for a task the bot can run.
	/// </summary>
	public abstract class BaseTask
	{
		private Uri m_heartbeatEndpoint;
		private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60f);
		private Thread m_heartbeatThread;

		protected Dictionary<string, HeartbeatData> m_heartbeatTasks = new Dictionary<string, HeartbeatData>();

		private static string HeartbeatEndpointPath
		{
			get { return Path.Combine(Configuration.DataDirectory, "heartbeat_endpoint.txt"); }
		}

		public readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();

		public BaseTask()
		{
			if (File.Exists(HeartbeatEndpointPath))
			{
				m_heartbeatEndpoint = new Uri(File.ReadAllText(HeartbeatEndpointPath));
				EasyWeb.SetDelayForDomain(m_heartbeatEndpoint, 0f);
			}
		}

		protected HeartbeatData AddHeartbeatTask(string taskKey)
		{
			HeartbeatData newData = new HeartbeatData(taskKey);
			m_heartbeatTasks.Add(taskKey, newData);
			return newData;
		}

		public void ExecuteLogged()
		{
			TaskLogger logger = new TaskLogger(this);
			try
			{
				Execute();
			}
			finally
			{
				logger.Close();
			}
		}

		/// <summary>
		/// Runs the task synchronously.
		/// </summary>
		public abstract void Execute();

		protected void StartHeartbeat()
		{
			if (m_heartbeatTasks.Count > 0 && !string.IsNullOrEmpty(m_heartbeatEndpoint.OriginalString))
			{
				m_heartbeatThread = new Thread(HeartbeatThread);
				m_heartbeatThread.Start();
			}
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
				foreach (HeartbeatData heartbeatTask in m_heartbeatTasks.Values)
				{
					string dataString;

					lock (heartbeatTask)
					{
						string serialized;
						heartbeatTask.terminate = terminate;
						serialized = JsonConvert.SerializeObject(heartbeatTask);
						dataString = "d=" + System.Web.HttpUtility.UrlEncode(serialized);
					}

					using (StreamReader response = new StreamReader(EasyWeb.Post(CreateHeartbeatRequest, dataString)))
					{
						//string text = response.ReadToEnd();
						//Console.WriteLine(text);
					}

					lock (heartbeatTask)
					{
						// edits are additive
						heartbeatTask.nEdits = 0;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		private HttpWebRequest CreateHeartbeatRequest()
		{
			return (HttpWebRequest)WebRequest.Create(m_heartbeatEndpoint);
		}
	}
}
