using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Does a null edit on all pages with incoming links to a set of pages.
	/// </summary>
	public class NullEditLinks : BaseTask
	{
		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "NullEditLinks"); }
		}

		public NullEditLinks()
		{
			Parameters["Page"] = "Creator:Chatsam";
		}

		public override void Execute()
		{
			Api api = GlobalAPIs.Commons;
			EasyWeb.SetDelayForDomain(api.Domain, 0.1f);

			List<PageTitle> links = new List<PageTitle>();

			Article article = new Article(PageTitle.Parse(Parameters["Page"]));
			foreach (Article link in article.GetLinksHere(api))
			{
				Article linkpage = api.GetPage(link);
				api.EditPage(linkpage, "null edit", minor: true);
				Console.WriteLine(link.title);
				links.Add(link.title);
			}

			string logFile = Path.Combine(ProjectDataDirectory, "log.txt");
			File.WriteAllLines(logFile, links.Select(pt => pt.FullTitle).ToArray());
		}
	}
}
