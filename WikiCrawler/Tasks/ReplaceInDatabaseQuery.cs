using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Tasks
{
	/// <summary>
	/// Performs a replacement operation on all files in a specified database query.
	/// Uses 'Params["Query"]' to specify the query.
	/// </summary>
	public abstract class ReplaceInDatabaseQuery : ReplaceIn
	{
		public ReplaceInDatabaseQuery(params BaseReplacement[] replacements)
			: base(replacements)
		{

		}

		/// <summary>
		/// Returns a new connection to the database to query.
		/// </summary>
		public abstract SQLiteConnection ConnectDatabase(bool bWantsWrite);

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			SQLiteConnection connection = ConnectDatabase(false);
			SQLiteCommand query = connection.CreateCommand();
			query.CommandText = Parameters["Query"];

			List<Article> results = new List<Article>();
			using (SQLiteDataReader reader = query.ExecuteReader())
			{
				while (reader.Read())
				{
					results.Add(new Article(PageTitle.Parse(reader.GetString(0))));
				}
			}

			connection.Close();
			return results;
		}
	}
}
