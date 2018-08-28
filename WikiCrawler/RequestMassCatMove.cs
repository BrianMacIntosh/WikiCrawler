using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	class RequestMassCatMove
	{
		public static void Do()
		{
			Wikimedia.WikiApi commonsApi = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org"));

			List<string> output = new List<string>();

			foreach (string catname in File.ReadAllLines("C:/Users/Brian/Desktop/revival3.csv", Encoding.Default))
			{
				string catnameReal = catname.Replace('_', ' ');

				Console.WriteLine(catnameReal);

				Wikimedia.Article catpage = commonsApi.GetPage("Category:" + catnameReal);

				EasyWeb.crawlDelay = 0.1f;

				if (!catpage.missing && catpage.revisions != null && !catpage.revisions[0].text.Contains("{{Category redirect|"))
				{
					output.Add("{{move cat|" + catnameReal + "|" + catnameReal.Replace("revival", "Revival") +
						"|3=[[Commons:Categories for discussion/2012/01/Revival architectural styles]]|user=BMacZero}}");
				}
			}

			File.WriteAllLines("C:/Users/Brian/Desktop/requests3.txt", output.ToArray(), Encoding.Default);
		}
	}
}
