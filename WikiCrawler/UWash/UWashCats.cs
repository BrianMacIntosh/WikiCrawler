using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikiCrawler;

namespace Tasks
{
	public class UWashCats : BaseTask
	{
		private static Dictionary<string, PageTitle> categoryMap = new Dictionary<string, PageTitle>();

		private static int completed = 0;

		public override void Execute()
		{
			//load mapped categories
			if (File.Exists("uwash_cats.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("uwash_cats.txt", FileMode.Open)))
				{
					while (!reader.EndOfStream)
					{
						string[] line = reader.ReadLine().Split('|');
						categoryMap[line[0].ToLower()] = PageTitle.Parse(line[1]);
					}
				}
			}

			Api Api = GlobalAPIs.Commons;

			throw new NotImplementedException(); //TODO: update
			CategoryMapping categoryMapping = null;// new CategoryMapping();

			string[] keys = categoryMap.Keys.ToArray();
			foreach (string key in keys)
			{
				if (categoryMap[key].IsEmpty
					//error fixup
					|| !categoryMap[key].IsNamespace(PageTitle.NS_Category))
				{
					Console.WriteLine(key + "?");

					PageTitle automap = categoryMapping.TranslateCategory(Api, key, new TaskItemKeyString()); //TODO: key
					if (!automap.IsEmpty)
					{
						Console.WriteLine("=" + automap);
						categoryMap[key] = automap;
						completed++;
						Save();
						continue;
					}
					else
					{
						Console.WriteLine("-automap failed");
					}

					/*Console.Write(':');
					string read = Console.ReadLine();
					if (read == "quit")
					{
						break;
					}
					else
					{
						categoryMap[key] = read;
						completed++;
						Save();
					}*/
				}
			}

			Console.WriteLine("Successfully mapped " + completed + " new cats.");
		}

		private static void Save()
		{
			//write to file
			using (StreamWriter writer = new StreamWriter(new FileStream("uwash_cats.txt", FileMode.Create)))
			{
				foreach (KeyValuePair<string, PageTitle> kv in categoryMap)
				{
					writer.WriteLine(kv.Key + "|" + kv.Value);
				}
			}
		}
	}
}
