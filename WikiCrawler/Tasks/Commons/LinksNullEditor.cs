using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaWiki;

namespace WikiCrawler
{
	public class LinksNullEditor
	{
		public static void Do()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			Api Api = new Api(commons);
			
			Api.AutoLogIn();

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
				foreach (Article link in article.GetLinksHere(Api))
				{
					Article linkpage = Api.GetPage(link);
					Api.SetPage(linkpage, "null edit", true, true, true);
					Console.WriteLine(link.title);
					links.Add(link.title);
				}
			}

			System.IO.File.WriteAllLines("E:/temp.txt", links.ToArray());
		}

		public static void CheckCat()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			Api Api = new Api(commons);
			Api.AutoLogIn();

			foreach (Article article in Api.GetCategoryEntries("Category:Images from the Asahel Curtis Photo Company Photographs Collection", cmtype: CMType.file))
			{
				Console.WriteLine(article.title);
				Article fullArticle = Api.GetPage(article);
				Article revs = Api.GetPage(article, rvprop: RVProp.user, rvlimit: Limit.Max);
				if (!fullArticle.revisions[0].text.Contains("[[Category:Images from the Asahel Curtis Photo Company Photographs Collection to check]]")
					&& !revs.revisions.Any(rev => rev.user == "Jmabel"))
				{
					fullArticle.revisions[0].text = WikiUtils.AddCategory(
						"Category:Images from the Asahel Curtis Photo Company Photographs Collection to check",
						fullArticle.revisions[0].text);
					Api.SetPage(fullArticle, "add check category to possibly unchecked images", true, true, true);
				}
				else
				{
					Console.WriteLine("MISSED");
					continue;
				}
			}
		}

		public static void Revert()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			Api Api = new Api(commons);
			Api.AutoLogIn();

			string startTime = "2018-09-22 16:00:00"; //very generous
			string endTime = "2018-09-22 17:20:00";

			List<int> badrevs = File.ReadAllLines("E:/temp.txt")
				.Where(l => !string.IsNullOrEmpty(l))
				.Select(l => int.Parse(l))
				.ToList();

			try
			{
				foreach (Contribution contrib in Api.GetContributions("BMacZero", endTime, startTime))
				{
					if (contrib.comment == "replace city with recognized name - Doing 1 replacements."
						&& !badrevs.Contains(contrib.revid))
					{
						Console.WriteLine(contrib.title);
						badrevs.Add(contrib.revid);
						Api.UndoRevision(contrib.pageid, contrib.revid, true);
					}
				}
			}
			finally
			{
				File.WriteAllLines("E:/temp.txt", badrevs.Select(i => i.ToString()).ToArray());
			}
		}
	}
}
