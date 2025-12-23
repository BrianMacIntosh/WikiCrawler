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
				  new PdArtReplacement()
				  //new LicenseHeaderCleanup(),
				  )
		{
			ImplicitCreatorsReplacement.SkipCached = false;
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time

			//TODO: SELECT * from files WHERE irreplaceableLicenses LIKE "PD-US-unpublished" AND replaced!=1

			Parameters["Query"] = "SELECT * FROM files WHERE replaced=0 AND latestYear=9999 AND authorDeathYear<2025-95-120 AND touchTimeUnix<1749831847";
			//Parameters["Query"] = "SELECT 'File:Gerard ter Borch Westfälischer Friede Kopie Münster.jpg'";
			//Parameters["Query"] = "SELECT pageTitle FROM files where pdArtLicense LIKE \"{{pd-art}}\" AND replaced=0";
		}

		public override SQLiteConnection ConnectDatabase(bool bWantsWrite)
		{
			return PdArtReplacement.ConnectFilesDatabase(false);
		}
	}
}
