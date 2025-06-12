using MediaWiki;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using WikiCrawler;

namespace Tasks.Commons
{
	/// <summary>
	/// Re-runs license replacement on files where the license was replaced with an imprecise year (ending in 9s due to the latest year being used).
	/// </summary>
	public class ImpreciseLicenseYearCheck : ReplaceIn
	{
		public ImpreciseLicenseYearCheck()
			: base(new PdArtReplacement())
		{
			ImplicitCreatorsReplacement.SlowCategoryWalk = false;
			PdArtReplacement.RerunGoodLicenses = true;
			PdArtReplacement.SkipCached = false;

			Parameters["Query"] = "SELECT pageTitle,authorString,authorQid,artQid,newLicense,dateString FROM files WHERE (authorDeathYear%10)=9 AND authorDeathYear!=9999 AND replaceTime<1748302257 AND (touchTimeUnix IS NULL OR touchTimeUnix<1749749935) AND replaced=1";
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			PdArtReplacement replacement = (PdArtReplacement)m_replacements[0];

			SQLiteCommand query = replacement.FilesDatabase.CreateCommand();
			query.CommandText = Parameters["Query"];

			using (SQLiteDataReader reader = query.ExecuteReader())
			{
				while (reader.Read())
				{
					PageTitle pageTitle = PageTitle.Parse(reader.GetString(0));
					ConsoleUtility.WriteLine(ConsoleColor.White, $"Checking {pageTitle}...");

					// check that newLicense has an imprecise year
					string newLicense = reader.IsDBNull(4) ? "" : reader.GetString(4);
					string newDeathyear = WikiUtils.GetTemplateParameter("deathyear", newLicense);
					if (string.IsNullOrEmpty(newDeathyear))
					{
						ConsoleUtility.WriteLine(ConsoleColor.Gray, "\tNo deathyear in license.");
						continue;
					}

					CreatorData authorData;

					if (!reader.IsDBNull(2))
					{
						// author QID is known
						authorData = WikidataCache.GetPersonData(new QId(reader.GetInt32(2)));
					}
					else
					{
						CommonsFileData fileData = new CommonsFileData()
						{
							PageTitle = pageTitle,
							Author = reader.GetString(1),
							Wikidata = reader.IsDBNull(3) ? "" : new QId(reader.GetInt32(3)).ToString(),
							Date = reader.GetString(5),
						};
						authorData = replacement.GetAuthorData(fileData);
					}

					if (authorData == null || authorData.DeathYear == null)
					{

					}
					else if (authorData.DeathYear.Precision < MediaWiki.DateTime.YearPrecision)
					{
						ConsoleUtility.WriteLine(ConsoleColor.Red, "\tImprecise deathyear in license.");
						yield return new Article(pageTitle);
					}
				}
			}
		}
	}
}
