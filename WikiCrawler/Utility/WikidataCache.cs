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
		public QId QID = QId.Empty;

		public MediaWiki.DateTime DeathYear = null;
		public QId CountryOfCitizenship;
		public PageTitle CommonsCategory;
	}

	public struct ArtworkData
	{
		public QId CreatorQid;
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
			return GetCreatorData(creatorTemplate, out eat);
		}

		/// <summary>
		/// Gets cached information about a creator.
		/// </summary>
		/// <param name="creatorTemplate">The creator template.</param>
		public static CreatorData GetCreatorData(string creatorTemplate)
		{
			CreatorTemplate creator = CreatorUtility.GetCreatorTemplate(creatorTemplate);
			if (creator.Template.IsEmpty)
			{
				throw new ArgumentException("Not a creator template name.", "creatorTemplate");
			}

			bool eat;
			return GetCreatorData(creator.Template, out eat);
		}

		/// <summary>
		/// Gets cached information about a creator.
		/// </summary>
		/// <param name="creatorTemplate">The creator template.</param>
		public static CreatorData GetCreatorData(PageTitle creatorTemplate, out bool isNew)
		{
			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "SELECT p.qid,p.deathYear,p.countryOfCitizenship,p.commonsCategory,p.deathYearPrecision FROM people p, creatortemplates c WHERE p.qid=c.qid AND c.templateName=$templateName";
			command.Parameters.AddWithValue("templateName", creatorTemplate.ToStringNormalized());
			using (var reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					isNew = false;
					return new CreatorData()
					{
						QID = new QId(reader.GetInt32(0)),
						DeathYear = reader.IsDBNull(1) ? null : MediaWiki.DateTime.FromYear(reader.GetInt32(1), reader.GetInt32(4)),
						CountryOfCitizenship = reader.IsDBNull(2) ? QId.Empty : new QId(reader.GetInt32(2)),
						CommonsCategory = reader.IsDBNull(3) ? PageTitle.Empty : PageTitle.Parse(reader.GetString(3)),
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
		public static CreatorData GetPersonData(QId qid)
		{
			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "SELECT deathYear,deathYearPrecision,countryOfCitizenship,commonsCategory FROM people WHERE qid=$qid";
			command.Parameters.AddWithValue("qid", qid.Id);
			using (var reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					return new CreatorData()
					{
						QID = qid,
						DeathYear = reader.IsDBNull(0) ? null : MediaWiki.DateTime.FromYear(reader.GetInt32(0), reader.GetInt32(1)),
						CountryOfCitizenship = reader.IsDBNull(2) ? QId.Empty : new QId(reader.GetInt32(2)),
						CommonsCategory = reader.IsDBNull(3) ? PageTitle.Empty : PageTitle.Parse(reader.GetString(3)),
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
		private static CreatorData RecordNewCreator(PageTitle creatorTemplate)
		{
			Article article = GlobalAPIs.Commons.GetPage(creatorTemplate);
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
				QId qid = QId.SafeParse(worksheet.Wikidata);

				// point creator and all redirects at the person qid
				foreach (Article creatorArticle in creatorArticles)
				{
					SQLiteCommand command = LocalDatabase.CreateCommand();
					command.CommandText = "INSERT INTO creatortemplates (templateName,qid,timestamp) VALUES ($templateName,$qid,unixepoch()) " +
						"ON CONFLICT(templateName) DO UPDATE SET qid=$qid,timestamp=unixepoch()";
					command.Parameters.AddWithValue("templateName", creatorArticle.title);
					command.Parameters.AddWithValue("qid", qid.Id);
					command.ExecuteNonQuery();
				}

				if (!qid.IsEmpty)
				{
					// person already cached?
					{
						SQLiteCommand command = LocalDatabase.CreateCommand();
						command.CommandText = "SELECT deathYear,deathYearPrecision,countryOfCitizenship,commonsCategory FROM people WHERE qid=$qid";
						command.Parameters.AddWithValue("qid", qid.Id);
						using (var reader = command.ExecuteReader())
						{
							if (reader.Read())
							{
								return new CreatorData()
								{
									QID = qid,
									DeathYear = reader.IsDBNull(0) ? null : MediaWiki.DateTime.FromYear(reader.GetInt32(0), reader.GetInt32(1)),
									CountryOfCitizenship = reader.IsDBNull(2) ? QId.Empty : new QId(reader.GetInt32(2)),
									CommonsCategory = reader.IsDBNull(3) ? PageTitle.Empty : PageTitle.Parse(reader.GetString(3)),
								};
							}
						}
					}

					// person is not yet cached
					return FetchNewPerson(qid);
				}
			}

			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		private static CreatorData FetchNewPerson(QId qid)
		{
			Entity entity = GlobalAPIs.Wikidata.GetEntity(qid);
			if (entity.missing || !entity.GetClaimValuesAsEntityIds(Wikidata.Prop_InstanceOf).Any(q => q == Wikidata.Entity_Human))
			{
				return new CreatorData();
			}
			else
			{
				//TODO: if no deathdate but another date (e.g. floruit) that is very old, record that (using deathYear 10000)

				MediaWiki.DateTime deathYear = GetCreatorDeathYear(entity);
				QId countryOfCitizenship = GetCreatorCountryOfCitizenship(entity);
				PageTitle commonsCategory = GetCreatorCommonsCategory(entity);

				SQLiteCommand command = LocalDatabase.CreateCommand();
				command.CommandText = "INSERT INTO people (qid,deathYear,deathYearPrecision,countryOfCitizenship,commonsCategory,timestamp) " +
					"VALUES ($qid,$deathYear,$deathYearPrecision,$countryOfCitizenship,$commonsCategory,unixepoch());";
				command.Parameters.AddWithValue("qid", qid.Id);
				command.Parameters.AddWithValue("deathYear", deathYear == null ? null : (int?)deathYear.GetLatestYear());
				command.Parameters.AddWithValue("deathYearPrecision", deathYear == null ? 0 : deathYear.Precision);
				command.Parameters.AddWithValue("countryOfCitizenship", (int?)countryOfCitizenship);
				command.Parameters.AddWithValue("commonsCategory", commonsCategory);
				command.ExecuteNonQuery();

				return new CreatorData()
				{
					QID = qid,
					DeathYear = deathYear,
					CountryOfCitizenship = countryOfCitizenship,
					CommonsCategory = commonsCategory,
				};
			}
		}

		public static MediaWiki.DateTime GetCreatorDeathYear(Entity entity)
		{
			if (entity.claims.TryGetValue(Wikidata.Prop_DateOfDeath, out Claim[] deathDates))
			{
				return GetLatestDateTime(deathDates);
			}

			return null;
		}

		public static QId GetCreatorCountryOfCitizenship(Entity entity)
		{
			//TODO: respect rank
			if (entity.HasClaim(Wikidata.Prop_CountryOfCitizenship))
			{
				return entity.GetClaimValueAsEntityId(Wikidata.Prop_CountryOfCitizenship);
			}

			return QId.Empty;
		}

		public static PageTitle GetCreatorCommonsCategory(Entity entity)
		{
			//TODO: respect rank
			if (entity.HasClaim(Wikidata.Prop_CommonsCategory))
			{
				return new PageTitle(PageTitle.NS_Category, entity.GetClaimValueAsString(Wikidata.Prop_CommonsCategory));
			}

			return PageTitle.Empty;
		}

		/// <summary>
		/// Gets cached information about an artwork.
		/// </summary>
		public static ArtworkData GetArtworkData(QId qid)
		{
			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "SELECT creatorQid,latestYear,timestamp FROM artwork WHERE qid=$qid";
			command.Parameters.AddWithValue("qid", qid.Id);
			using (var reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					return new ArtworkData()
					{
						CreatorQid = reader.IsDBNull(0) ? QId.Empty : new QId(reader.GetInt32(0)),
						LatestYear = reader.IsDBNull(1) ? 9999 : reader.GetInt32(1),
					};
				}
				else
				{
					return FetchNewArtwork(qid);
				}
			}
		}

		/// <summary>
		/// Deletes the specified artwork from the cache.
		/// </summary>
		public static void InvalidateArtwork(QId qid)
		{
			SQLiteCommand command = LocalDatabase.CreateCommand();
			command.CommandText = "DELETE FROM artwork WHERE qid=$qid";
			command.Parameters.AddWithValue("qid", qid.Id);
			command.ExecuteNonQuery();
		}

		/// <summary>
		/// 
		/// </summary>
		private static ArtworkData FetchNewArtwork(QId qid)
		{
			Entity entity = GlobalAPIs.Wikidata.GetEntity(qid);
			if (entity.missing)
			//TODO: check type: || entity.GetClaimValueAsEntityId(Wikidata.Prop_InstanceOf) != Wikidata.Entity_Human)
			{
				return new ArtworkData();
			}
			else
			{
				QId creatorQid = GetArtworkCreator(entity);
				int? latestYear = GetArtworkLatestYear(entity);

				SQLiteCommand command = LocalDatabase.CreateCommand();
				command.CommandText = "INSERT INTO artwork (qid,creatorQid,latestYear,timestamp) " +
					"VALUES ($qid,$creatorQid,$latestYear,unixepoch()) " +
					"ON CONFLICT(qid) DO UPDATE SET creatorQid=$creatorQid,latestYear=$latestYear,timestamp=unixepoch();";
				command.Parameters.AddWithValue("qid", qid.Id);
				command.Parameters.AddWithValue("creatorQid", (int?)creatorQid);
				command.Parameters.AddWithValue("latestYear", latestYear);
				command.ExecuteNonQuery();

				return new ArtworkData()
				{
					CreatorQid = creatorQid,
					LatestYear = latestYear.HasValue ? latestYear.Value : 9999,
				};
			}
		}

		public static QId GetArtworkCreator(Entity entity)
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

			return QId.Empty;
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
		/// Returns the latest possible date time from a set of date values. This may not be the most precise.
		/// </summary>
		public static MediaWiki.DateTime GetLatestDateTime(Claim[] dateClaims)
		{
			IEnumerable<Claim> bestClaims = Wikidata.KeepBestRank(dateClaims);
			if (bestClaims.Any())
			{
				int latestYear = int.MinValue;
				MediaWiki.DateTime latestDateTime = null;
				foreach (Claim claim in bestClaims)
				{
					int year = GetLatestYear(claim);
					if (year > latestYear)
					{
						latestYear = year;
						latestDateTime = GetLatestDateTime(claim);
					}
				}
				return latestDateTime;
			}
			else
			{
				return null;
			}
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
				return bestClaims.Max(claim => GetLatestYear(claim));
			}
			else
			{
				return 9999;
			}
		}

		/// <summary>
		/// Returns the latest possible year represented by a <see cref="Claim"/>.
		/// </summary>
		public static int GetLatestYear(Claim claim)
		{
			if (claim == null)
			{
				return 9999;
			}
			else
			{
				MediaWiki.DateTime date = claim.mainSnak.GetValueAsDate();
				int latestDate = GetLatestYear(date);

				// also consider Latest Date qualifiers
				Snak[] latestQual = claim.GetQualifiers(Wikidata.Qualifier_LatestDate);
				if (latestQual != null)
				{
					latestDate = Math.Min(latestDate, GetLatestYear(latestQual));
				}

				return latestDate;
			}
		}

		/// <summary>
		/// Returns the latest possible time represented by a <see cref="Claim"/>.
		/// </summary>
		public static MediaWiki.DateTime GetLatestDateTime(Claim claim)
		{
			if (claim == null)
			{
				return MediaWiki.DateTime.FromYear(9999, MediaWiki.DateTime.YearPrecision);
			}
			else
			{
				MediaWiki.DateTime mainDate = claim.mainSnak.GetValueAsDate();
				int latestDate = GetLatestYear(mainDate);

				// also consider Latest Date qualifiers
				Snak[] latestQuals = claim.GetQualifiers(Wikidata.Qualifier_LatestDate);
				if (latestQuals != null)
				{
					//TODO:
				}

				return mainDate;
			}
		}

		/// <summary>
		/// Returns the latest possible year represented by a DateTime.
		/// </summary>
		/// <returns></returns>
		public static int GetLatestYear(Snak snak)
		{
			if (snak == null)
			{
				return 9999;
			}
			else
			{
				return GetLatestYear(snak.GetValueAsDate());
			}
		}

		/// <summary>
		/// Returns the latest possible year represented by a DateTime.
		/// </summary>
		/// <returns></returns>
		public static int GetLatestYear(Snak[] snaks)
		{
			if (snaks == null)
			{
				return 9999;
			}
			else
			{
				return snaks.Max(snak => GetLatestYear(snak));
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
			else
			{
				return time.GetLatestYear();
			}
		}
	}
}
