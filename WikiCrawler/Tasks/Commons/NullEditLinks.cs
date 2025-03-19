using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;

namespace Tasks
{
	/// <summary>
	/// Does a null edit on all pages with incoming links to a set of pages.
	/// </summary>
	public class NullEditLinks : BaseTask
	{
		public override void Execute()
		{
			Api api = GlobalAPIs.Commons;
			EasyWeb.SetDelayForDomain(api.Domain, 0.1f);
			api.AutoLogIn();

			string[] pages = new string[]
			{
				"Creator:Chatsam",
				"Creator:Allen & Ginter",
				"Creator:Tim Felce (Airwolfhound)",
				"Creator:Thomas R Machnitzki"
			};

			List<string> links = new List<string>();

			foreach (string page in pages)
			{
				Article article = new Article(page);
				foreach (Article link in article.GetLinksHere(api))
				{
					Article linkpage = api.GetPage(link);
					api.EditPage(linkpage, "null edit", minor: true);
					Console.WriteLine(link.title);
					links.Add(link.title);
				}
			}

			File.WriteAllLines("E:/temp.txt", links.ToArray());
		}
	}
}
