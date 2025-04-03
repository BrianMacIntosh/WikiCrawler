using MediaWiki;
using System;

namespace Tasks
{
	/// <summary>
	/// Produces a list of users who have created subcategories of a given category.
	/// </summary>
	public class FindCategoryCreators : BaseTask
	{
		public FindCategoryCreators()
		{
			Parameters["Category"] = "Category:Ships by name (flat list)";
		}

		public override void Execute()
		{
			Api Api = GlobalAPIs.Commons;
			
			foreach (Article article in Api.GetCategoryEntries(Parameters["Category"], cmtype: CMType.subcat))
			{
				Article articleContent = Api.GetPage(article);
				throw new NotImplementedException();
			}
		}
	}
}
