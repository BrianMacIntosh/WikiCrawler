using MediaWiki;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Tasks.Commons
{
	/// <summary>
	/// Replaces PD-Art tags with imprecise licenses with a more specific license, if one can be determined.
	/// </summary>
	public class PdArtFixup : ReplaceInCategories
	{
		public PdArtFixup()
			: base(
				new ImplicitCreatorsReplacement("PdArtReplacement"),
				new LocalizeDateReplacement(),
				new PdArtReplacement()
				//, new FixInformationTemplates()
				)
		{

		}

		public override IEnumerable<PageTitle> GetCategories()
		{
			return new PageTitle[]
			{
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-US)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-US-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-50)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-50-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-60)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-60-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-70)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-70-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-75)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-75-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-80)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-80-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-90))"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-90-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-95)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-95-expired)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-100)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old default)"),
				new PageTitle(PageTitle.NS_Category, "PD-Art (PD-old-auto)"),
			};
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			if (PdArtReplacement.SkipCached)
			{
				SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(false);
				return LoggedWhere(base.GetPagesToAffectUncached(startSortkey),
					article => !PdArtReplacement.IsFileCached(connection, article.title));
			}
			else
			{
				return base.GetPagesToAffectUncached(startSortkey);
			}
		}

		private static IEnumerable<Article> LoggedWhere(IEnumerable<Article> articles, Predicate<Article> predicate)
		{
			int failCount = 0;
			foreach (Article article in articles)
			{
				if (predicate(article))
				{
					if (failCount > 0)
					{
						ConsoleUtility.WriteLine(ConsoleColor.DarkYellow, "[Skipped {0} cached files]", failCount);
						failCount = 0;
					}
					yield return article;
				}
				else
				{
					failCount++;
				}
			}
			if (failCount > 0)
			{
				ConsoleUtility.WriteLine(ConsoleColor.DarkYellow, "[Skipped {0} cached files]", failCount);
			}
		}
	}
}
