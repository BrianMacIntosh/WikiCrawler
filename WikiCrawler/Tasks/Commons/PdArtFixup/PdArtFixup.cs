using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Tasks.Commons
{
	/// <summary>
	/// Replaces PD-Art tags with imprecise licenses with a more specific license, if one can be determined.
	/// </summary>
	public class PdArtFixup : ReplaceInCategory
	{
		public PdArtFixup()
			: base(
				new ImplicitCreatorsReplacement("PdArtReplacement"),
				//new LocalizeDateReplacement(),
				new PdArtReplacement()
				//, new FixInformationTemplates()
				)
		{
			Parameters.Add("Category", "Category:PD-Art (PD-old-auto)");
		}

		public override string GetCategory()
		{
			return Parameters["Category"];
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			if (PdArtReplacement.SkipCached)
			{
				SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(false);
				return base.GetPagesToAffectUncached(startSortkey)
					.Where(article => !PdArtReplacement.IsFileCached(connection, PageTitle.Parse(article.title)));
			}
			else
			{
				return base.GetPagesToAffectUncached(startSortkey);
			}
		}
	}
}
