using MediaWiki;
using System;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Writes a list of pages in a specified category to a file.
	/// </summary>
	public class DownloadCategoryList : BaseTask
	{
		public DownloadCategoryList()
		{
			//Parameters["Category"] = "Category:Primary license tags (flat list)";
			//Parameters["OutputFile"] = "primary-license-tags.txt";

			Parameters["Category"] = "Category:Language templates";
			Parameters["OutputFile"] = "language-templates.txt";
		}

		public override void Execute()
		{
			using (StreamWriter writer = new StreamWriter(Path.Combine(Configuration.DataDirectory, Parameters["OutputFile"])))
			{
				foreach (Article page in GlobalAPIs.Commons.GetCategoryEntries(Parameters["Category"], CMType.page))
				{
					if (page.title.StartsWith("Template:"))
					{
						Console.WriteLine(page.title);
						writer.WriteLine(page.title.Substring("Template:".Length));
					}
				}
			}
		}
	}
}
