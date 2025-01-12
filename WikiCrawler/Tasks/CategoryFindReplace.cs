using MediaWiki;
using System;

namespace Tasks
{
	public class CategoryFindReplace : BaseTask
	{
		public override void Execute()
		{
			EasyWeb.crawlDelay = 0.0f;

			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org"));

			Console.WriteLine("Logging in...");
			commonsApi.AutoLogIn();

			foreach (Article page in commonsApi.GetCategoryEntries("Category:Fictitious symbol related deletion requests", CMType.page))
			{
				Console.WriteLine(page.title);
				Article pagePulled = commonsApi.GetPage(page);
				string newText = pagePulled.revisions[0].text.Replace(
					"\n[[Category:Fictitious symbol related deletion requests]]",
					"\n<noinclude>[[Category:Fictitious symbol related deletion requests]]</noinclude>");
				if (newText != pagePulled.revisions[0].text)
				{
					pagePulled.revisions[0].text = newText;
					commonsApi.SetPage(pagePulled, "add noinclude around category", bot: false);
				}
			}
		}
	}
}
