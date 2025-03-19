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
		public override void Execute()
		{
			using (StreamWriter writer = new StreamWriter(Path.Combine(Configuration.DataDirectory, "primary-license-tags.txt")))
			{
				foreach (Article page in GlobalAPIs.Commons.GetCategoryEntries("Category:Primary license tags (flat list)", CMType.page))
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
