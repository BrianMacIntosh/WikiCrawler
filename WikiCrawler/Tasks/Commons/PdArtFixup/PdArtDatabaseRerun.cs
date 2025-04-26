using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Tasks.Commons
{
	/// <summary>
	/// Reruns the <see cref="PdArtReplacement"/> on files from a SQLite query.
	/// </summary>
	public class PdArtDatabaseRerun : ReplaceIn
	{
		public PdArtDatabaseRerun()
			: base(
				  //new ImplicitCreatorsReplacement("FixImplicitCreators"),
				  new LocalizeDateReplacement(),
				  new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time

			//TODO: SELECT * from files WHERE irreplaceableLicenses LIKE "PD-US-unpublished" AND bLicenseReplaced!=1
			//Parameters["Query"] = "SELECT pageTitle from files WHERE irreplaceableLicenses LIKE \"PD-US-unpublished\" AND bLicenseReplaced!=1";

			Parameters["Query"] = "SELECT * FROM files where authorString=\"\" AND dateString=\"\" and artQid IS NULL and touchTimeUnix<1745687521 AND bLicenseReplaced!=1";

			//Parameters["Query"] = "SELECT pageTitle FROM files where pdArtLicense LIKE \"{{pd-art}}\" AND bLicenseReplaced=0";
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(false);
			SQLiteCommand query = connection.CreateCommand();
			query.CommandText = Parameters["Query"];

			List<Article> results = new List<Article>();
			using (SQLiteDataReader reader = query.ExecuteReader())
			{
				while (reader.Read())
				{
					results.Add(new Article(reader.GetString(0)));
				}
			}

			connection.Close();
			return results;
		}
	}
}
