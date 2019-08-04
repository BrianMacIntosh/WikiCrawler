using MediaWiki;
using System;

namespace Tasks
{
	public class FindCategoryCreators
	{
		/// <summary>
		/// Produces a list of users who have created subcategories of a given category.
		/// </summary>
		[BatchTask]
		public static void Find()
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
