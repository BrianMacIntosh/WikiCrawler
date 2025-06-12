using System.Data.SQLite;

namespace Tasks.Commons
{
	/// <summary>
	/// Reruns the <see cref="PdArtReplacement"/> on files from a SQLite query.
	/// </summary>
	public class PdArtDatabaseRerun : ReplaceInDatabaseQuery
	{
		public PdArtDatabaseRerun()
			: base(
				  new ImplicitCreatorsReplacement("FixImplicitCreators"),
				  new LocalizeDateReplacement(),
				  new PdArtReplacement())
		{
			ImplicitCreatorsReplacement.SkipCached = false;
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time

			//TODO: SELECT * from files WHERE irreplaceableLicenses LIKE "PD-US-unpublished" AND replaced!=1
			//Parameters["Query"] = "SELECT pageTitle from files WHERE irreplaceableLicenses LIKE \"PD-US-unpublished\" AND replaced!=1";

			Parameters["Query"] = "SELECT * FROM files WHERE replaced=0 AND latestYear<1850 AND (authorString=\"{{unknown|author}}\" OR authorString=\"{{unknown|artist}}\" OR authorString=\"{{unknown|1=author}}\" OR authorString=\"{{unknown|1=artist}}\" OR authorString=\"{{unknown|author}}\" OR authorString=\"{{unknown author}}\" OR authorString=\"{{author|unknown}}\" OR authorString=\"{{unknown photographer}}\" OR authorString=\"{{unknown|photographer}}\" OR authorString=\"{{creator:unknown}}\" OR authorString=\"{{creator:?}}\")";

			//Parameters["Query"] = "SELECT pageTitle FROM files where pdArtLicense LIKE \"{{pd-art}}\" AND replaced=0";
		}

		public override SQLiteConnection ConnectDatabase(bool bWantsWrite)
		{
			return PdArtReplacement.ConnectFilesDatabase(false);
		}
	}
}
