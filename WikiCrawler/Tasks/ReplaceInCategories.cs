using MediaWiki;
using System;
using System.Collections.Generic;

namespace Tasks
{
	/// <summary>
	/// Performs a replacement operation on all files in a particular category.
	/// </summary>
	public abstract class ReplaceInCategories : ReplaceIn
	{
		// checkpoints don't work with multiple cats
		protected override bool UseCheckpoint => !GetCategories().AnyX(2);

		public ReplaceInCategories(params BaseReplacement[] replacement)
			: base(replacement)
		{

		}

		/// <summary>
		/// Returns the names of the categories to affect.
		/// </summary>
		public abstract IEnumerable<string> GetCategories();

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
			return GetPagesHelper(GetCategories(), GetCMType(), startSortkey, IsRecursive());
		}

		public static IEnumerable<Article> GetPagesHelper(IEnumerable<string> categories, string pageType, string startSortKey, bool recursive)
		{
			foreach (string category in categories)
			{
				int count = 0;
				ConsoleUtility.WriteLine(ConsoleColor.Cyan, "ReplaceInCategories on category '{0}'.", category);
				if (recursive)
				{
					IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntriesRecursive(category, cmtype: pageType, cmstartsortkeyprefix: startSortKey);
					foreach (Article article in allFiles)
					{
						yield return article;
						count++;
					}
				}
				else
				{
					IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries(category, pageType, cmstartsortkeyprefix: startSortKey);
					foreach (Article article in allFiles)
					{
						yield return article;
						count++;
					}
				}
				ConsoleUtility.WriteLine(ConsoleColor.Cyan, "  {0} files.", count);
			}
		}
	}
}
