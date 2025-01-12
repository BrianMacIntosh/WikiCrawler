using MediaWiki;
using System;

namespace Tasks
{
	/// <summary>
	/// Produces a list of users who have created subcategories of a given category.
	/// </summary>
	public class FindCategoryCreators : BaseTask
	{
		public override void Execute()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			Api Api = new Api(commons);
			
			Api.AutoLogIn();

			foreach (Article article in Api.GetCategoryEntries("Category:Ships by name (flat list)", cmtype: CMType.subcat))
			{
				Article articleContent = Api.GetPage(article);
				throw new NotImplementedException();
			}
		}
	}
}
