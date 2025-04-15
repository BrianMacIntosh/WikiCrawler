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
				  new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time
			PdArtReplacement.SkipWikidataLookups = true; // already tried last time

			Parameters["Query"] = "SELECT pageTitle from files where authorString LIKE \"{{c|%}}\" and bLicenseReplaced!=1";
			//Parameters["Query"] = "SELECT pageTitle from files WHERE irreplaceableLicenses LIKE \"PD-US-unpublished\" AND bLicenseReplaced!=1";

			// recache files where art qid is not yet cached (I think)
			//Parameters["Query"] = "SELECT pageTitle FROM files where bLicenseReplaced=0 AND (touchTimeUnix < 1743182781 OR touchTimeUnix IS NULL)";

			//Parameters["Query"] = "SELECT pageTitle FROM files where pdArtLicense LIKE \"{{pd-art}}\" AND bLicenseReplaced=0";
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			return GlobalAPIs.Commons.FetchArticles(GetDummyArticles());
		}

		private IEnumerable<Article> GetDummyArticles()
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
