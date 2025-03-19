using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Base class for a task the bot can run.
	/// </summary>
	public abstract class BaseTask
	{
		public bool HeartbeatEnabled = false;

		private Uri m_heartbeatEndpoint;
		private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60f);
		private Thread m_heartbeatThread;

		protected Dictionary<string, object> m_heartbeatData = new Dictionary<string, object>();

		private static string HeartbeatEndpointPath
		{
			get { return Path.Combine(Configuration.DataDirectory, "heartbeat_endpoint.txt"); }
		}

		public BaseTask()
		{
			if (File.Exists(HeartbeatEndpointPath))
			{
				m_heartbeatEndpoint = new Uri(File.ReadAllText(HeartbeatEndpointPath));
				EasyWeb.SetDelayForDomain(m_heartbeatEndpoint, 0f);
			}

			m_heartbeatData["taskKey"] = GetType().Name;
			m_heartbeatData["nEdits"] = 0;
			m_heartbeatData["terminate"] = false;
		}

		/// <summary>
		/// Runs the task synchronously.
		/// </summary>
		public abstract void Execute();

		protected void StartHeartbeat()
		{
			if (HeartbeatEnabled && !string.IsNullOrEmpty(m_heartbeatEndpoint.OriginalString))
			{
				m_heartbeatThread = new Thread(HeartbeatThread);
				m_heartbeatThread.Start();
			}
		}

		private void HeartbeatThread()
		{
			SendHeartbeat(false);
			Thread.Sleep(HeartbeatInterval);
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
				string serialized;
				lock (m_heartbeatData)
				{
					m_heartbeatData["terminate"] = terminate;
					serialized = JsonConvert.SerializeObject(m_heartbeatData);
				}
				string dataString = "d=" + System.Web.HttpUtility.UrlEncode(serialized);
				Stream response = EasyWeb.Post(CreateHeartbeatRequest, dataString);
				response.Dispose();

				// edits are additive
				m_heartbeatData["nEdits"] = 0;
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
