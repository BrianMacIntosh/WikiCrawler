using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;

namespace WikiCrawler
{
	class Program
    {
        static void Main(string[] args)
        {
			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
				   | SecurityProtocolType.Tls11
				   | SecurityProtocolType.Tls12
				   | SecurityProtocolType.Ssl3;
			
			try
			{
				Console.Write("Task>");
				string task = Console.ReadLine();

				// disable sleep while task runs
				WindowsUtility.SetThreadExecutionState(WindowsUtility.EXECUTION_STATE.ES_SYSTEM_REQUIRED | WindowsUtility.EXECUTION_STATE.ES_CONTINUOUS);

				switch (task)
				{
					case "watermarkdl":
						new WatermarkRemoval("Category:Images from Slovenian Ethnographic Museum website").Download();
						break;
					case "watermarkfind":
						new WatermarkRemoval("Category:Images from Slovenian Ethnographic Museum website").FindWatermark();
						break;
					case "watermarkremove":
						new WatermarkRemoval("Category:Images from Slovenian Ethnographic Museum website").RemoveAndUpload();
						break;
					case "npassets":
						{
							NPGallery.NPGalleryAssetListDownloader albumDl = new NPGallery.NPGalleryAssetListDownloader("npgallery");
							albumDl.DownloadAll();
						}
						break;
					case "batchrebuild":
						BatchController.RebuildSuccesses();
						break;
					case "batchrevalidate":
						BatchController.RevalidateDownloads();
						break;
					case "batchdownall":
						BatchController.DownloadAll();
						break;
					case "batchdown":
						BatchController.Download();
						break;
					case "batchup":
						BatchController.Upload();
						break;
					default:
						{
							// look for the class that contains the task
							Type taskType = taskType = typeof(Program).Assembly.GetType("Tasks." + task);
							if (taskType == null)
							{
								Console.WriteLine("Could not find a class for that task.");
								break;
							}
							List<MethodInfo> taskMethods = new List<MethodInfo>();
							foreach (MethodInfo method in taskType.GetMethods(BindingFlags.Static | BindingFlags.Public))
							{
								if (method.GetCustomAttributes<BatchTaskAttribute>().Any())
								{
									Console.WriteLine((taskMethods.Count + 1).ToString() + ". " + method.Name);
									taskMethods.Add(method);
								}
							}

							// select a method on that class
							MethodInfo runMe;
							if (taskMethods.Count == 0)
							{
								Console.WriteLine("Could not find any BatchTask methods on that class.");
								break;
							}
							else if (taskMethods.Count == 1)
							{
								runMe = taskMethods[0];
							}
							else
							{
								ConsoleKeyInfo key = Console.ReadKey();
								while (key.Key < ConsoleKey.D1
									|| key.Key > ConsoleKey.D9
									|| (key.Key - ConsoleKey.D1) >= taskMethods.Count)
								{
									key = Console.ReadKey();
								}
								Console.WriteLine();
								runMe = taskMethods[key.Key - ConsoleKey.D1];
							}

							runMe.Invoke(null, null);
						}
						break;
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

			Console.WriteLine("Done");
			WindowsUtility.FlashWindowEx(Process.GetCurrentProcess().MainWindowHandle);

			// shut down
			//WindowsUtility.DoExitWin(WindowsUtility.EWX.EWX_SHUTDOWN);

			Console.ReadLine();
        }
    }
}
