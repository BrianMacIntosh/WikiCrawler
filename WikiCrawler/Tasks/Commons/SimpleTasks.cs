using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaWiki;

namespace Tasks
{
	/// <summary>
	/// Class for simple, self contained tasks.
	/// </summary>
	public class SimpleTasks
	{
		/// <summary>
		/// Does a null edit on all pages with incoming links to a set of pages.
		/// </summary>
		[BatchTask]
		public static void NullEditLinks()
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
					Api.EditPage(linkpage, "null edit", minor: true);
					Console.WriteLine(link.title);
					links.Add(link.title);
				}
			}

			File.WriteAllLines("E:/temp.txt", links.ToArray());
		}

		/// <summary>
		/// Adds a check category to files that haven't been checked yet.
		/// </summary>
		[BatchTask]
		public static void AddCheckCategory()
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
					Api.EditPage(fullArticle, "add check category to possibly unchecked images", minor: true);
				}
				else
				{
					Console.WriteLine("MISSED");
					continue;
				}
			}
		}

		/// <summary>
		/// Reverts a range of contributions.
		/// </summary>
		[BatchTask]
		public static void RevertContribs()
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
