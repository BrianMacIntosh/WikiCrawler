using MediaWiki;
using System;
using System.Linq;

namespace Tasks.Commons
{
	/// <summary>
	/// Adds a check category to files that haven't been checked yet.
	/// </summary>
	public class AddCheckCategory : BaseTask
	{
		public AddCheckCategory()
		{
			Parameters["ToCategory"] = "Category:Images from the Asahel Curtis Photo Company Photographs Collection";
			Parameters["CheckCategory"] = "Category:Images from the Asahel Curtis Photo Company Photographs Collection to check";
		}

		public override void Execute()
		{
			Api Api = GlobalAPIs.Commons;
			foreach (Article article in Api.GetCategoryEntries(PageTitle.Parse(Parameters["ToCategory"]), cmtype: CMType.file))
			{
				Console.WriteLine(article.title);
				Article fullArticle = Api.GetPage(article, rvprop: RVProp.user, rvlimit: Limit.Max);
				if (!WikiUtils.HasCategory(PageTitle.Parse(Parameters["CheckCategory"]), fullArticle.revisions[0].text)
					&& !fullArticle.revisions.Any(rev => rev.user == "Jmabel"))
				{
					fullArticle.revisions[0].text = WikiUtils.AddCategory(Parameters["CheckCategory"], fullArticle.revisions[0].text);
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
