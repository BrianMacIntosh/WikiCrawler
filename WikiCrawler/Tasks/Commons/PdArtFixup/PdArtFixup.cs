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

		public override IEnumerable<string> GetCategories()
		{
			return new string[]
			{
				"Category:PD-Art (PD-US)",
				"Category:PD-Art (PD-US-expired)",
				"Category:PD-Art (PD-old-50)",
				"Category:PD-Art (PD-old-50-expired)",
				"Category:PD-Art (PD-old-60)",
				"Category:PD-Art (PD-old-60-expired)",
				"Category:PD-Art (PD-old-70)",
				"Category:PD-Art (PD-old-70-expired)",
				"Category:PD-Art (PD-old-75)",
				"Category:PD-Art (PD-old-75-expired)",
				"Category:PD-Art (PD-old-80)",
				"Category:PD-Art (PD-old-80-expired)",
				"Category:PD-Art (PD-old-90))",
				"Category:PD-Art (PD-old-90-expired)",
				"Category:PD-Art (PD-old-95)",
				"Category:PD-Art (PD-old-95-expired)",
				"Category:PD-Art (PD-old-100)",
				"Category:PD-Art (PD-old)",
				"Category:PD-Art (PD-old default)",
				"Category:PD-Art (PD-old-auto)",
			};
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			if (PdArtReplacement.SkipCached)
			{
				SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(false);
				return LoggedWhere(base.GetPagesToAffectUncached(startSortkey),
					article => !PdArtReplacement.IsFileCached(connection, PageTitle.Parse(article.title)));
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
