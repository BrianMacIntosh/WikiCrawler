using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wikimedia;

namespace WikiCrawler
{
	public class FindCategoryCreators
	{
		public static void Find()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			WikiApi Api = new WikiApi(commons);

			Console.WriteLine("Logging in...");
			Credentials credentials = Configuration.LoadCredentials();
			Api.LogIn(credentials.Username, credentials.Password);

			foreach (Article article in Api.GetCategoryEntries("Category:Ships by name (flat list)", "subcat"))
			{
				Article articleContent = Api.GetPage(article);
			}
		}
	}
}
