using MediaWiki;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace WikiCrawler
{
	//TODO: struct?
	public class CreatorData
	{
		public int DeathYear = 9999;
		public int? CountryOfCitizenship;
		public string CommonsCategory;
	}

	public struct ArtworkData
	{
		public int? CreatorQid;
		public int LatestYear;
	}

	public static class WikidataCache
	{
		/// <summary>
		/// Database caching various useful information from Wikidata.
		/// </summary>
		public static readonly SQLiteConnection LocalDatabase;

		public static string CacheDatabaseFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "wikidata.db"); }
		}

		static WikidataCache()
		{
			SQLiteConnectionStringBuilder connectionString = new SQLiteConnectionStringBuilder
			{
				{ "Data Source", CacheDatabaseFile },
				{ "Mode", "ReadWrite" }
			};
			LocalDatabase = new SQLiteConnection(connectionString.ConnectionString);
			LocalDatabase.Open();
		}

		/// <summary>
		/// Gets cached information about a creator.
		/// </summary>
		/// <param name="creatorTemplate">The creator template.</param>
		public static CreatorData GetCreatorData(PageTitle creatorTemplate)
		{
			bool eat;
			return GetCreatorData(creatorTemplate.ToString(), out eat);
		}

		/// <summary>
		/// Gets cached information about a creator.
		/// </summary>
		/// <param name="creatorTemplate">The creator template.</param>
		public static CreatorData GetCreatorData(string creatorTemplate)
		{
			bool eat;
			return GetCreatorData(creatorTemplate, out eat);
		}

		/// <summary>
		/// Gets cached information about a creator.
		/// </summary>
		/// <param name="creatorTemplate">The creator template.</param>
		public static CreatorData GetCreatorData(string creatorTemplate, out bool isNew)
		{
			PageTitle creatorPage = CreatorUtility.GetCreatorTemplate(creatorTemplate);
			if (creatorPage.IsEmpty)
			{
				throw new ArgumentException("Not a creator template name.", "creatorTemplate");
			}

			creatorTemplate = creatorPage.ToString();

			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "SELECT p.deathYear,p.countryOfCitizenship,p.commonsCategory FROM people p, creatortemplates c WHERE p.qid=c.qid AND c.templateName=$templateName";
			command.Parameters.AddWithValue("templateName", creatorTemplate);
			using (var reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					isNew = false;
					return new CreatorData()
					{
						DeathYear = reader.GetInt32(0),
						CountryOfCitizenship = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1),
						CommonsCategory = reader.IsDBNull(2) ? null : reader.GetString(2),
					};
				}
				else
				{
					isNew = true;
					return RecordNewCreator(creatorTemplate);
				}
			}
		}

		/// <summary>
		/// Gets cached information about a person.
		/// </summary>
		public static CreatorData GetPersonData(int qid)
		{
			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "SELECT deathYear,countryOfCitizenship,commonsCategory FROM people WHERE qid=$qid";
			command.Parameters.AddWithValue("qid", qid);
			using (var reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					return new CreatorData()
					{
						DeathYear = reader.GetInt32(0),
						CountryOfCitizenship = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1),
						CommonsCategory = reader.IsDBNull(2) ? null : reader.GetString(2),
					};
				}
				else
				{
					return FetchNewPerson(qid);
				}
			}
		}

		/// <summary>
		/// Caches data for a new creator template.
		/// </summary>
		private static CreatorData RecordNewCreator(string creatorTemplate)
		{
			Article article = GlobalAPIs.Commons.GetPage(creatorTemplate.ToString());
			if (Article.IsNullOrMissing(article))
			{
				return null;
			}

			List<Article> creatorArticles = new List<Article> { article };

			// follow redirects
			while (true)
			{
				Article newArticle = GlobalAPIs.Commons.GetRedirectTarget(article);
				if (newArticle == null)
				{
					break;
				}
				if (newArticle.missing)
				{
					return null;
				}
				creatorArticles.Add(newArticle);
				article = newArticle;
			}

			if (!Article.IsNullOrEmpty(article))
			{
				CommonsCreatorWorksheet worksheet = new CommonsCreatorWorksheet(article);
				string wikidata = worksheet.Wikidata;
				int? qid = Wikidata.UnQidify(wikidata);

				// point creator and all redirects at the person qid
				foreach (Article creatorArticle in creatorArticles)
				{
					SQLiteCommand command = LocalDatabase.CreateCommand();
					command.CommandText = "INSERT INTO creatortemplates (templateName,qid,timestamp) VALUES ($templateName,$qid,unixepoch()) " +
						"ON CONFLICT(templateName) DO UPDATE SET qid=$qid,timestamp=unixepoch()";
					command.Parameters.AddWithValue("templateName", creatorArticle.title);
					command.Parameters.AddWithValue("qid", qid);
					command.ExecuteNonQuery();
				}

				if (qid.HasValue)
				{
					// person already cached?
					{
						SQLiteCommand command = LocalDatabase.CreateCommand();
						command.CommandText = "SELECT deathYear,countryOfCitizenship,commonsCategory FROM people WHERE qid=$qid";
						command.Parameters.AddWithValue("qid", qid.Value);
						using (var reader = command.ExecuteReader())
						{
							if (reader.Read())
							{
								return new CreatorData()
								{
									DeathYear = reader.GetInt32(0),
									CountryOfCitizenship = reader.IsDBNull(1) ? null : (int?)reader.GetInt32(1),
									CommonsCategory = reader.IsDBNull(2) ? null : reader.GetString(2),
								};
							}
						}
					}

					// person is not yet cached
					return FetchNewPerson(qid.Value);
				}
			}

			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		private static CreatorData FetchNewPerson(int qid)
		{
			Entity entity = GlobalAPIs.Wikidata.GetEntity("Q" + qid);
			if (entity.missing || entity.GetClaimValueAsEntityId(Wikidata.Prop_InstanceOf) != Wikidata.Entity_Human)
			{
				return new CreatorData();
			}
			else
			{
				//TODO: if no deathdate but another date (e.g. floruit) that is very old, record that (using deathYear 10000)

				int deathYear = GetCreatorDeathYear(entity);
				int? countryOfCitizenship = GetCreatorCountryOfCitizenship(entity);
				string commonsCategory = GetCreatorCommonsCategory(entity);

				SQLiteCommand command = LocalDatabase.CreateCommand();
				command.CommandText = "INSERT INTO people (qid,deathYear,countryOfCitizenship,commonsCategory,timestamp) " +
					"VALUES ($qid,$deathYear,$countryOfCitizenship,$commonsCategory,unixepoch());";
				command.Parameters.AddWithValue("qid", qid);
				command.Parameters.AddWithValue("deathYear", deathYear);
				command.Parameters.AddWithValue("countryOfCitizenship", countryOfCitizenship);
				command.Parameters.AddWithValue("commonsCategory", commonsCategory);
				command.ExecuteNonQuery();

				return new CreatorData()
				{
					DeathYear = deathYear,
					CountryOfCitizenship = countryOfCitizenship,
					CommonsCategory = commonsCategory,
				};
			}
		}

		public static int GetCreatorDeathYear(Entity entity)
		{
			if (entity.claims.TryGetValue(Wikidata.Prop_DateOfDeath, out Claim[] deathDates))
			{
				return GetLatestYear(deathDates);
			}

			return 9999;
		}

		public static int? GetCreatorCountryOfCitizenship(Entity entity)
		{
			//TODO: respect rank
			if (entity.HasClaim(Wikidata.Prop_CountryOfCitizenship))
			{
				return entity.GetClaimValueAsEntityId(Wikidata.Prop_CountryOfCitizenship);
			}

			return null;
		}

		public static string GetCreatorCommonsCategory(Entity entity)
		{
			//TODO: respect rank
			if (entity.HasClaim(Wikidata.Prop_CommonsCategory))
			{
				return entity.GetClaimValueAsString(Wikidata.Prop_CommonsCategory);
			}

			return null;
		}

		/// <summary>
		/// Gets cached information about an artwork.
		/// </summary>
		public static ArtworkData GetArtworkData(string qid)
		{
			return GetArtworkData(Wikidata.UnQidifyChecked(qid));
		}

		/// <summary>
		/// Gets cached information about an artwork.
		/// </summary>
		public static ArtworkData GetArtworkData(int qid)
		{
			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "SELECT creatorQid,latestYear,timestamp FROM artwork WHERE qid=$qid";
			command.Parameters.AddWithValue("qid", qid);
			using (var reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					int timestamp = reader.GetInt32(2);
					if (timestamp < 1745687317)
					{
						// invalidated cache
						return FetchNewArtwork(qid);
					}
					else
					{
						return new ArtworkData()
						{
							CreatorQid = reader.IsDBNull(0) ? null : (int?)reader.GetInt32(0),
							LatestYear = reader.IsDBNull(1) ? 9999 : reader.GetInt32(1),
						};
					}
				}
				else
				{
					return FetchNewArtwork(qid);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private static ArtworkData FetchNewArtwork(int qid)
		{
			Entity entity = GlobalAPIs.Wikidata.GetEntity("Q" + qid);
			if (entity.missing)
			//TODO: check type: || entity.GetClaimValueAsEntityId(Wikidata.Prop_InstanceOf) != Wikidata.Entity_Human)
			{
				return new ArtworkData();
			}
			else
			{
				int? creatorQid = GetArtworkCreator(entity);
				int? latestYear = GetArtworkLatestYear(entity);

				SQLiteCommand command = LocalDatabase.CreateCommand();
				command.CommandText = "INSERT INTO artwork (qid,creatorQid,latestYear,timestamp) " +
					"VALUES ($qid,$creatorQid,$latestYear,unixepoch()) " +
					"ON CONFLICT(qid) DO UPDATE SET creatorQid=$creatorQid,latestYear=$latestYear,timestamp=unixepoch();";
				command.Parameters.AddWithValue("qid", qid);
				command.Parameters.AddWithValue("creatorQid", creatorQid);
				command.Parameters.AddWithValue("latestYear", latestYear);
				command.ExecuteNonQuery();

				return new ArtworkData()
				{
					CreatorQid = creatorQid,
					LatestYear = latestYear.HasValue ? latestYear.Value : 9999,
				};
			}
		}

		public static int? GetArtworkCreator(Entity entity)
		{
			if (entity.claims.TryGetValue(Wikidata.Prop_Creator, out Claim[] creators))
			{
				IEnumerable<Claim> bestCreators = Wikidata.KeepBestRank(creators).Where(claim => claim.mainSnak.datavalue != null);
				if (bestCreators.Count() == 1)
				{
					return bestCreators.Select(claim => claim.mainSnak.GetValueAsEntityId()).First();
				}
				else
				{
					//TODO: handle multiple
				}
			}

			return null;
		}

		public static int GetArtworkLatestYear(Entity entity)
		{
			if (entity.claims.TryGetValue(Wikidata.Prop_PublicationDate, out Claim[] pubDates))
			{
				return GetLatestYear(pubDates);
			}
			else if (entity.claims.TryGetValue(Wikidata.Prop_Inception, out Claim[] inceptDates))
			{
				return GetLatestYear(inceptDates);
			}

			return 9999;
		}

		/// <summary>
		/// Returns the latest possible year represented by a set of date values.
		/// </summary>
		/// <returns></returns>
		public static int GetLatestYear(Claim[] dateClaims)
		{
			IEnumerable<Claim> bestClaims = Wikidata.KeepBestRank(dateClaims);
			if (bestClaims.Any())
			{
				return bestClaims.Select(claim => claim.mainSnak.GetValueAsDate()).Max(date => GetLatestYear(date));
			}
			else
			{
				return 9999;
			}
		}

		/// <summary>
		/// Returns the latest possible year represented by a DateTime.
		/// </summary>
		/// <returns></returns>
		public static int GetLatestYear(MediaWiki.DateTime time)
		{
			if (time == null)
			{
				return 9999;
			}
			else if (time.Precision >= MediaWiki.DateTime.YearPrecision)
			{
				return time.GetYear();
			}
			else if (time.Precision == MediaWiki.DateTime.DecadePrecision)
			{
				return GetLatestYear(time.GetYear(), 1);
			}
			else if (time.Precision == MediaWiki.DateTime.CenturyPrecision)
			{
				return GetLatestYear(time.GetYear(), 2);
			}
			else if (time.Precision == MediaWiki.DateTime.MilleniumPrecision)
			{
				return GetLatestYear(time.GetYear(), 3);
			}
			else
			{
				return 9999;
			}
		}

		public static int GetLatestYear(int value, int impreciseDigits)
		{
			int quanta = (int)Math.Pow(10, impreciseDigits);
			int significantDigits = (int)Math.Ceiling(value / (double)quanta);
			return (significantDigits + 1) * quanta - 1;
		}
	}
}
