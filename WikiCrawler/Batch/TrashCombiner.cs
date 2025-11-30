using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using Tasks;

namespace WikiCrawler
{
	/// <summary>
	/// Pulls data from a backup or old folder to rebuild data cache/trash for a project.
	/// </summary>
	public class TrashCombiner : BaseTask
	{
		/// <summary>
		/// Other data_cache directory to pull data from.
		/// </summary>
		private const string OtherDirectory = @"E:\Brian\Downloads\old_wikidata\warner\data_cache";

		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);
			string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(Path.Combine(projectDir, "succeeded.json")));

			string trashDirectory = Path.Combine(projectDir, "data_trash");
			Directory.CreateDirectory(trashDirectory);

			foreach (string file in Directory.GetFiles(OtherDirectory))
			{
				string filename = Path.GetFileName(file);
				string key = Path.GetFileNameWithoutExtension(file);
				string newTrashPath = Path.Combine(trashDirectory, filename);
				string newCachePath = Path.Combine(projectDir, "data_cache", filename);
				if (File.Exists(newTrashPath))
				{
					if (succeeded.Contains(key))
					{
						ConsoleUtility.WriteLine(ConsoleColor.White, $"'{key}' is already in the trash.");
						File.Delete(file);
					}
					else
					{
						ConsoleUtility.WriteLine(ConsoleColor.Red, $"'{key}' is in the trash but not succeeded.");
					}
				}
				else if (File.Exists(newCachePath))
				{
					if (succeeded.Contains(key))
					{
						ConsoleUtility.WriteLine(ConsoleColor.Yellow, $"'{key}' is in the cache but is succeeded.");
						File.Move(newCachePath, newTrashPath);
					}
					else
					{
						ConsoleUtility.WriteLine(ConsoleColor.White, $"'{key}' is already in the cache.");
					}
				}
				else
				{
					if (succeeded.Contains(key))
					{
						ConsoleUtility.WriteLine(ConsoleColor.Green, $"Moving '{key}' to the trash.");
						File.Move(file, newTrashPath);
					}
					else
					{
						ConsoleUtility.WriteLine(ConsoleColor.Green, $"Moving '{key}' to the cache.");
						File.Move(file, newCachePath);
					}
				}
			}
		}
	}
}
