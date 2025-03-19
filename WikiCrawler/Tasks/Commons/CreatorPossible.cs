using MediaWiki;
using System;
using System.Collections.Generic;

namespace Tasks.Commons
{
	/// <summary>
	/// Removes {{creator possible}} from categories that do have a creator.
	/// </summary>
	public class CreatorPossible : ReplaceInCategory
	{
		public CreatorPossible()
			: base(new CreatorPossibleReplacement())
		{
			
		}

		public override string GetCMType()
		{
			return CMType.subcat;
		}

		public override string GetCategory()
		{
			return "Category:Creator template possible";
		}
	}

	public class CreatorPossibleReplacement : BaseReplacement
	{
		private ImplicitCreatorsReplacement m_implicitCreators;

		public CreatorPossibleReplacement()
		{
			m_implicitCreators = new ImplicitCreatorsReplacement("CreatorPossible");
		}

		public override bool DoReplacement(Article article)
		{
			//OPT: will fetch page again
			PageTitle creator = ImplicitCreatorsReplacement.GetCreatorFromCommonsPage(null, PageTitle.Parse(article.title));
			if (creator.IsEmpty)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  No creator.");
				return false;
			}
			else
			{
				ConsoleUtility.WriteLine(ConsoleColor.Green, "  Found creator, removing template.");

				string entityId = ImplicitCreatorsReplacement.GetEntityIdForCreator(creator);
				Entity entity = GlobalAPIs.Wikidata.GetEntity(entityId);

				if (entity != null)
				{
					// try to use the creator in any categorized files
					IEnumerable<Article> subArticles = GlobalAPIs.Commons.GetCategoryEntriesRecursive(article.title, 4, cmtype: CMType.file);
					foreach (Article subArticle in GlobalAPIs.Commons.FetchArticles(subArticles))
					{
						ConsoleUtility.WriteLine(ConsoleColor.White, "  {0}", subArticle.title);

						if (m_implicitCreators.DoReplacement(subArticle, entity))
						{
							if (subArticle.Dirty)
							{
								GlobalAPIs.Commons.EditPage(subArticle, subArticle.GetEditSummary());
							}
						}
					}
				}

				article.Changes.Add("Removing {{creator possible}} because creator exists.");
				article.Dirty = true;
				article.revisions[0].text = WikiUtils.RemoveTemplate("creator possible", article.revisions[0].text);

				return true;
			}
		}

		public override void SaveOut()
		{
			base.SaveOut();

			m_implicitCreators.SaveOut();
		}
	}
}
