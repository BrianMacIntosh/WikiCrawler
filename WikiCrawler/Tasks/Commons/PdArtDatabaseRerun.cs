using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Tasks.Commons
{
	public class PdArtDatabaseRerun : ReplaceIn
	{
		public PdArtDatabaseRerun()
			: base(new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time
			PdArtReplacement.SkipAuthorLookup = true; // already tried last time

			HeartbeatEnabled = true;
			m_heartbeatData["taskKey"] = "PdArtFollowup";
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			return GlobalAPIs.Commons.FetchArticles(GetDummyArticles());
		}

		private IEnumerable<Article> GetDummyArticles()
		{
			SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(false);
			SQLiteCommand query = connection.CreateCommand();
			query.CommandText = "SELECT pageTitle FROM files WHERE bLicenseReplaced=2";

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
