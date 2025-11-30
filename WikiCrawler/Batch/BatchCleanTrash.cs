using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Task that moves succeeded data out of the cache and into the trash.
	/// </summary>
	public class BatchCleanTrash : BaseTask
	{
		/// <summary>
		/// If set, if data is already in the trash, overwrite it.
		/// </summary>
		private const bool Overwrite = false;

		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);
			string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine(projectDir, "succeeded.json")));
			DoClean(projectDir, succeeded);
		}

		public static void DoClean(string projectDir, IList<string> succeeded)
		{
			string trashDirectory = Path.Combine(projectDir, "data_trash");
			Directory.CreateDirectory(trashDirectory);

			foreach (string id in succeeded)
			{
				string metadataFile = Path.Combine(projectDir, "data_cache", id + ".json");
				if (File.Exists(metadataFile))
				{
					Console.WriteLine($"'{id}' is still cached even though it's succeeded.");

					//TODO: get filenames from central source
					string targetFile = Path.Combine(trashDirectory, id + ".json");
					if (File.Exists(targetFile) && !Overwrite)
					{
						ConsoleUtility.WriteLine(ConsoleColor.Yellow, $"'{id}' is already in the trash: deleting.");
						File.Delete(metadataFile);
					}
					else
					{
						ConsoleUtility.WriteLine(ConsoleColor.Green, $"Moving '{id}' to the trash.");
						if (Overwrite)
						{
							File.Delete(targetFile);
						}
						File.Move(metadataFile, targetFile);
					}
				}
			}

		}
	}
}
