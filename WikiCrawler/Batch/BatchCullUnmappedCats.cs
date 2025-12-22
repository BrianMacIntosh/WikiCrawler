using System;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Task that removes unmapped entries from a specified project's category mappings.
	/// </summary>
	public class BatchCullUnmappedCats : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Project Key>");
			string projectKey = Console.ReadLine();
			string projectDir = Path.Combine(Configuration.DataDirectory, projectKey);
			CategoryMapping mapping = new CategoryMapping(Path.Combine(projectDir, "categories.json"));
			List<string> killList = new List<string>();
			foreach (string key in mapping.Keys)
			{
				MappingCategory cat = mapping.TryGetValue(key);
				if (cat.IsUnmapped)
				{
					killList.Add(key);
				}
			}
			Console.WriteLine("Culling {0} keys.", killList.Count);
			foreach (string key in killList)
			{
				mapping.Remove(key);
			}
			mapping.Serialize();
		}
	}
}
