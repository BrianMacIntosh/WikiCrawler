﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikiCrawler;

namespace Tasks
{
	public class UWashCats : BaseTask
	{
		private static Dictionary<string, string> categoryMap = new Dictionary<string, string>();

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
						categoryMap[line[0].ToLower()] = line[1];
					}
				}
			}

			MediaWiki.Api Api = new MediaWiki.Api(new Uri("https://commons.wikimedia.org/"));

			string[] keys = categoryMap.Keys.ToArray();
			foreach (string key in keys)
			{
				if (string.IsNullOrEmpty(categoryMap[key])
					//error fixup
					|| !categoryMap[key].StartsWith("Category:"))
				{
					Console.WriteLine(key + "?");

					string automap = CategoryTranslation.TranslateCategory(Api, key);
					if (!string.IsNullOrEmpty(automap))
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
				foreach (KeyValuePair<string, string> kv in categoryMap)
				{
					writer.WriteLine(kv.Key + "|" + kv.Value);
				}
			}
		}
	}
}
