using MediaWiki;
using System;
using System.Diagnostics;
using System.Net;

namespace WikiCrawler
{
	class Program
    {
		static void Main(string[] _)
        {
			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
				   | SecurityProtocolType.Tls11
				   | SecurityProtocolType.Tls12
				   | SecurityProtocolType.Ssl3;

			Console.OutputEncoding = System.Text.Encoding.UTF8;
			Console.Write("Task>");
			string taskName = Console.ReadLine();

			try
			{
				// disable sleep while task runs
				WindowsUtility.SetThreadExecutionState(WindowsUtility.EXECUTION_STATE.ES_SYSTEM_REQUIRED | WindowsUtility.EXECUTION_STATE.ES_CONTINUOUS);

				// look for task class by name
				Type taskType = null;
				foreach (Type type in typeof(Program).Assembly.GetTypes())
				{
					if (typeof(Tasks.BaseTask).IsAssignableFrom(type))
					{
						if (type.Name.Equals(taskName, StringComparison.InvariantCultureIgnoreCase))
						{
							taskType = type;
							break;
						}
					}
				}

				if (taskType == null)
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "Could not find a class for that task.");
				}
				else
				{
					Console.WriteLine("Task class is '{0}'.", taskType.Name);

					Tasks.BaseTask taskInstance = (Tasks.BaseTask)Activator.CreateInstance(taskType);
					taskInstance.ExecuteLogged();
				}
			}
			/*catch (Exception e)
			{
				Console.WriteLine(e);
				using (StreamWriter writer = new StreamWriter(new FileStream("errorlog.txt", FileMode.Create)))
				{
					writer.WriteLine(e.ToString());
				}
			}*/
			finally
			{
				//TODO: check dirty
				CategoryTranslation.SaveOut();

				// allow sleep
				WindowsUtility.SetThreadExecutionState(WindowsUtility.EXECUTION_STATE.ES_CONTINUOUS);
			}

			ConsoleUtility.WriteLine(ConsoleColor.Cyan, "DONE.");

			WindowsUtility.FlashWindowEx(Process.GetCurrentProcess().MainWindowHandle);

			// shut down
			//WindowsUtility.DoExitWin(WindowsUtility.EWX.EWX_SHUTDOWN);

			Console.ReadLine();
        }
    }
}
