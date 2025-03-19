using MediaWiki;
using System;
using System.Linq;

namespace Tasks
{
	/// <summary>
	/// Adds a check category to files that haven't been checked yet.
	/// </summary>
	public class AddCheckCategory : BaseTask
	{
		public override void Execute()
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
	}
}
