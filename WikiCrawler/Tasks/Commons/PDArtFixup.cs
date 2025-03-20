using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Tasks
{
	/// <summary>
	/// Replaces PD-Art tags with imprecise licenses with a more specific license, if one can be determined.
	/// </summary>
	public class PdArtFixup : ReplaceInCategory
	{
		public static BaseReplacement CreateReplacement()
		{
			return new CompoundReplacementTask(new ImplicitCreatorsReplacement("PdArtReplacement"), new LocalizeDateReplacement(), new PdArtReplacement(), new FixInformationTemplates());
		}

		public PdArtFixup()
			: base(CreateReplacement())
		{
			HeartbeatEnabled = true;
		}

		public override string GetCategory()
		{
			//return "Category:PD-Art (PD-old default)";
			return "Category:PD-Art (PD-old-70)";
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
