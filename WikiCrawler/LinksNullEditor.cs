using System;
using Wikimedia;

namespace WikiCrawler
{
	public class LinksNullEditor
	{
		public static void Do()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			WikiApi Api = new WikiApi(commons);

			Console.WriteLine("Logging in...");
			Credentials credentials = Configuration.LoadCredentials();
			Api.LogIn(credentials.Username, credentials.Password);

			string[] pages = new string[]
			{
				"Creator:Wolfgang Sauber",
				"Creator:Thomas R Machnitzki (thomas@machnitzki.com)",
				"Creator:IFCAR",
				"Creator:Miscellaneous Items in High Demand, PPOC, Library of Congress"
			};

			foreach (string page in pages)
			{
				Article article = new Article(page);
				foreach (Article link in article.GetLinksHere(Api))
				{
					Article linkpage = Api.GetPage(link);
					Api.SetPage(linkpage, "null edit", true, true, true);
					Console.WriteLine(link.title);
				}
			}
		}
	}
}
