using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Tasks.Commons
{
	/// <summary>
	/// Reruns the <see cref="PdArtReplacement"/> on files from a SQLite query.
	/// </summary>
	public class PdArtInterwikiRerun : ReplaceIn
	{
		public PdArtInterwikiRerun()
			: base(
				  new ImplicitCreatorsReplacement("FixImplicitCreators"),
				  new LocalizeDateReplacement(),
				  new PdArtReplacement())
		{
			ImplicitCreatorsReplacement.SkipCached = false;
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(false);

			bool skip = true;

			foreach (var kv in GlobalAPIs.Commons.GetInterwikiMap())
			{
				if (kv.Key == "de")
					skip = false;
				if (skip) continue;

				ConsoleUtility.WriteLine(System.ConsoleColor.White, kv.Key);

				SQLiteCommand query = connection.CreateCommand();
				query.CommandText = string.Format("SELECT * FROM files WHERE bLicenseReplaced=0 AND (authorString LIKE \"%[[:{0}:%\" OR authorString LIKE \"%[[{0}:%\")", kv.Key);

				List<Article> articles = new List<Article>();
				using (SQLiteDataReader reader = query.ExecuteReader())
				{
					while (reader.Read())
					{
						articles.Add(new Article(PageTitle.Parse(reader.GetString(0))));
					}
				}
				foreach (Article article in articles)
				{
					yield return article;
				}
			}

			connection.Close();
		}
	}
}
