using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Tasks;

namespace WikiCrawler.Batch.UWash
{
	public class ProschFix : BaseTask
	{
		public override void Execute()
		{
			int count = 0;
			Regex idRegex = new Regex(@"\(PROSCH ([0-9]+)\)");
			foreach (Article file in GlobalAPIs.Commons.FetchArticles(GlobalAPIs.Commons.GetCategoryEntries(new PageTitle(PageTitle.NS_Category, "Images from the Prosch Albums"))))
			{
				Match match = idRegex.Match(file.title.Name);
				if (match.Success)
				{
					CommonsFileWorksheet ws = new CommonsFileWorksheet(file);
					if (ws.Author == "{{Creator:Thomas W. Prosch}}")
					{
						ConsoleUtility.WriteLine(ConsoleColor.White, $"Prosch author");
						string trashFile = Path.Combine(Configuration.DataDirectory, "prosch", "data_trash", match.Groups[1].Value + ".json");
						Dictionary<string, string> json = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(trashFile));
						if (!json.ContainsKey("Photographer"))
						{
							Console.WriteLine("Remove Prosch author");
							ws.Author = "{{unknown|photographer}}";
							GlobalAPIs.Commons.SetPage(file, "Replace Thomas Prosch with 'unknown photographer'");
							count++;
						}
						else
						{
							Console.WriteLine(json["Photographer"]);
						}
					}
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, $"No id for {file.title}");
				}
			}
			Console.WriteLine(count);
		}
	}
}
