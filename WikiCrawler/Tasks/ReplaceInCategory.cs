using MediaWiki;
using System.Collections.Generic;

namespace Tasks
{
	/// <summary>
	/// Performs a replacement operation on all files in a particular category.
	/// </summary>
	public abstract class ReplaceInCategory : ReplaceIn
	{
		public ReplaceInCategory(BaseReplacement replacement)
			: base(replacement)
		{

		}

		/// <summary>
		/// Returns the name of the category to affect.
		/// </summary>
		public abstract string GetCategory();

		/// <summary>
		/// Returns the type of category member to find.
		/// </summary>
		public virtual string GetCMType()
		{
			return CMType.file;
		}

		/// <summary>
		/// Returns true if the search should recurse child categories.
		/// </summary>
		public virtual bool IsRecursive()
		{
			return false;
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			return GetPagesHelper(GetCategory(), GetCMType(), startSortkey, IsRecursive());
		}

		public static IEnumerable<Article> GetPagesHelper(string category, string pageType, string startSortKey, bool recursive)
		{
			if (recursive)
			{
				IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntriesRecursive(category, cmtype: pageType, cmstartsortkeyprefix: startSortKey);
				foreach (Article article in GlobalAPIs.Commons.FetchArticles(allFiles))
				{
					yield return article;
				}
			}
			else
			{
				IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries(category, pageType, cmstartsortkeyprefix: startSortKey);
				foreach (Article article in GlobalAPIs.Commons.FetchArticles(allFiles))
				{
					yield return article;
				}
			}
		}
	}
}
