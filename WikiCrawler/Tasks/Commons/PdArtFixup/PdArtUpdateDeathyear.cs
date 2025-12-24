using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Tasks.Commons
{
	/// <summary>
	/// Fixup task that corrects the deathyears of pd-old-auto licenses from Wikidata.
	/// </summary>
	public class PdArtUpdateDeathyear : ReplaceInDatabaseQuery
	{
		public PdArtUpdateDeathyear()
			: base(new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			PdArtReplacement.AllowDeathyearOverwrite = true;
			PdArtReplacement.RerunGoodLicenses = true;

			Parameters["Query"] = "SELECT pageTitle,pdArtLicense,newLicense FROM files WHERE pdArtLicense LIKE \"%deathyear%\" AND newLicense LIKE \"%deathyear%\" AND replaced=1";
		}

		/// <summary>
		/// Returns a new connection to the database to query.
		/// </summary>
		public override SQLiteConnection ConnectDatabase(bool bWantsWrite)
		{
			return PdArtReplacement.ConnectFilesDatabase(false);
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			SQLiteConnection connection = ConnectDatabase(false);
			SQLiteCommand query = connection.CreateCommand();
			query.CommandText = Parameters["Query"];

			List<Article> result = new List<Article>();
			using (SQLiteDataReader reader = query.ExecuteReader())
			{
				while (reader.Read())
				{
					//TODO: make sure I'm not fighting another user over the deathyear
					PageTitle title = PageTitle.Parse(reader.GetString(0));
					string oldLicense = reader.GetString(1);
					string newLicense = reader.GetString(2);
					string oldDeathyear = WikiUtils.GetTemplateParameter("deathyear", oldLicense);
					string newDeathyear = WikiUtils.GetTemplateParameter("deathyear", newLicense);
					if (!string.IsNullOrEmpty(oldDeathyear) && oldDeathyear != newDeathyear)
					{
						result.Add(new Article(title));
					}
				}
			}

			connection.Close();
			return result;
		}
	}
}
