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
			: base(new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time
			PdArtReplacement.SkipAuthorLookup = true; // already tried last time

			HeartbeatEnabled = true;
			m_heartbeatData["taskKey"] = "PdArtFixup";

			Parameters["Query"] = "SELECT * FROM files WHERE (dateString LIKE \"executed in%\" OR dateString LIKE \"%(published)\") AND bLicenseReplaced!=1";
			//Parameters["Query"] = "SELECT * FROM files where bLicenseReplaced=0 AND (touchTimeUnix < 1743182781 OR touchTimeUnix IS NULL)";
			//Parameters["Query"] = "SELECT * FROM files where pdArtLicense LIKE \"{{pd-art}}\" AND bLicenseReplaced=0";
			//Parameters["Query"] = "SELECT pageTitle FROM files WHERE innerLicense LIKE \"PD-old-90\" AND bLicenseReplaced != 1 AND authorDeathYear != 9999";
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
