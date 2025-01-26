using MediaWiki;
using System.Collections.Generic;
using System.Linq;

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

		public override IEnumerable<Article> GetFilesToAffectUncached(string startSortkey)
		{
			IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries(GetCategory(), CMType.file, cmstartsortkeyprefix: startSortkey);
			while (true)
			{
				IEnumerable<Article> theseFiles = allFiles.Take(50);

				if (!theseFiles.Any())
				{
					break;
				}

				Article[] filesGot = GlobalAPIs.Commons.GetPages(theseFiles.ToList(), prop: "info|revisions");

				foreach (Article file in filesGot)
				{
					yield return file;
				}

				allFiles = allFiles.Skip(50);
			}
		}
	}
}
