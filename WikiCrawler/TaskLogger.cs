using System;
using System.IO;
using System.Linq;
using Tasks;

namespace WikiCrawler
{
	internal class TaskLogger
	{
		public static string LogFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "tasks.log"); }
		}

		private readonly BaseTask m_task;

		private DateTime m_startTime;

		public TaskLogger(BaseTask task)
		{
			m_task = task;
			m_startTime = DateTime.Now;

			File.AppendAllLines(LogFile, new string[]
			{
				"",
				string.Format("[{0}]: started {1}", m_startTime.ToString(), m_task.GetType().Name)
			});

			File.AppendAllLines(LogFile, task.Parameters.Select(kv => string.Format("\t{0}: {1}", kv.Key, kv.Value)));

			//TODO: log edit/upload count
		}

		public void Close()
		{
			File.AppendAllLines(LogFile, new string[]
			{
				string.Format("[{0}]: stopped {1}", DateTime.Now.ToString(), m_task.GetType().Name),
				string.Format("Elapsed: {0}", (DateTime.Now - m_startTime))
			});
		}
	}
}
