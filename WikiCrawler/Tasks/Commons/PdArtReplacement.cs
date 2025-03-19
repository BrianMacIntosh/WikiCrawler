using MediaWiki;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Replaces naked PD-Art with a more specific license, if one can be determined.
	/// </summary>
	public class PdArtReplacement : BaseReplacement
	{
		public enum ReplacementStatus
		{
			NotReplaced = 0,
			Replaced = 1,
			NotFound = 2,
		}

		/// <summary>
		/// If set, skips files that are already cached.
		/// </summary>
		public static bool SkipCached = true;

		/// <summary>
		/// If set, skips any attempts to look up author deathyears with extra queries.
		/// </summary>
		public static bool SkipAuthorLookup = false;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "PdArtReplacement"); }
		}

		public static string FilesDatabaseFile
		{
			get { return Path.Combine(ProjectDataDirectory, "files.db"); }
		}

		/// <summary>
		/// Database caching information about files that have been examined so far.
		/// </summary>
		private SQLiteConnection m_filesDatabase;

		private static List<Regex> s_dateRegexes = new List<Regex>();
		private static List<Regex> s_centuryRegexes = new List<Regex>();
		private static Regex s_sRegex = new Regex("^([0-9]+)'?[Ss]$");

		static PdArtReplacement()
		{
			foreach (string regex in File.ReadAllLines(Path.Combine(ProjectDataDirectory, "date-regexes.txt")))
			{
				if (!string.IsNullOrWhiteSpace(regex))
				{
					s_dateRegexes.Add(new Regex(regex));
				}
			}

			foreach (string regex in File.ReadAllLines(Path.Combine(ProjectDataDirectory, "century-regexes.txt")))
			{
				if (!string.IsNullOrWhiteSpace(regex))
				{
					s_centuryRegexes.Add(new Regex(regex));
				}
			}
		}

		private int qtyProcessed = 0;
		private int qtyInfoFindFail = 0;
		private int qtyDateParseFail = 0;
		private int qtyNotPDUS = 0;
		private int qtyNoCreator = 0;
		private int qtyNoDeathYear = 0;
		private int qtyInsufficientPMA = 0;
		private int qtyOtherLicense = 0;
		private int qtySuccess = 0;

		private ManualMapping<MappingDate> m_dateMapping;

		public static string DuplicateLicensesLogFile
		{
			get { return Path.Combine(ProjectDataDirectory, "duplicate-licenses.txt"); }
		}

		public static string DateMappingFile
		{
			get { return Path.Combine(ProjectDataDirectory, "date-mappings.txt"); }
		}

		private static readonly string[] s_pdArtTemplates = new string[]
		{
			"pd-art",
			"dp-art",
			"pd-art/en",
			"pd-art/pt-br",
			"pd-art-70",
			"pd-art-old-70",
			"pd-art-life-70",
			"pd-art-100",
			//"pd-art-old-100-expired",
			"pd-art-us",
			"pd-art-two",
			"licensed-pd-art",
			"licensed pd-art",
			"licensed-pd-art-two",
		};

		private static readonly string[] s_pmaLicenses = new string[]
		{
			"PD-old-50",
			"PD-old-50-expired",
			"PD-old-50-1923",
			"PD-old-60",
			"PD-old-60-expired",
			"PD-old-60-1923",
			"PD-old-70",
			"PD-old-70-expired",
			"PD-old-70-1923",
			"PD-old-75",
			"PD-old-75-expired",
			"PD-old-75-1923",
			"PD-old-80",
			"PD-old-80-expired",
			"PD-old-80-1923",
			"PD-old-90",
			"PD-old-90-expired",
			"PD-old-90-1923",
			"PD-old-95",
			"PD-old-95-expired",
			"PD-old-95-1923",
			"PD-old-100",
			"PD-old-100-expired",
			"PD-old-100-1923",
		};

		private static readonly string[] s_supersedeLicenses = new string[]
		{
			"PD-old",
			"PD-old-auto",
			"PD-old-50",
			"PD-old-50-expired",
			"PD-old-50-1923",
			"PD-old-60",
			"PD-old-60-expired",
			"PD-old-60-1923",
			"PD-old-70",
			"PD-old-70-expired",
			"PD-old-70-1923",
			"PD-old-75",
			"PD-old-75-expired",
			"PD-old-75-1923",
			"PD-old-80",
			"PD-old-80-expired",
			"PD-old-80-1923",
			"PD-old-90",
			"PD-old-90-expired",
			"PD-old-90-1923",
			"PD-old-95",
			"PD-old-95-expired",
			"PD-old-95-1923",
			"PD-old-100",
			"PD-old-100-expired",
			"PD-old-100-1923",
			"PD-old-assumed",
			"PD-old-assumed-expired",
			"PD-old-assumed/sandbox",
			"PD-old-auto-1923",
			"PD-old-auto-expired",
			"PD-US",
			"PD-US-expired",
			"PD-US-1923",
			"PD-US-1924",
			"PD-US-1925",
			"PD-US-1926",
			"PD-US-1927",
			"PD-US-1928",
			"PD-US-1929",
			"PD-US-1930",
			"PD-1923",
			"PD-1924",
			"PD-1925",
			"PD-1926",
			"PD-1927",
			"PD-1928",
			"PD-1929",
			"PD-1930",
			"Unclear-PD-US-old-70",
		};

		private static readonly Regex s_goodPdArtNakedRegex = new Regex(@"^\s*pd-art\|pd-old-auto-expired\|deathyear=[0-9]+\s*$", RegexOptions.IgnoreCase);

		//TODO: add more
		//TODO: implement me
		private static readonly string[] s_removeCats = new string[]
		{
			"[[Category:PD-Art (PD-old)]]",
			"[[Category:PD-Art (PD-old default)]]",
			"[[Category:PD Old]]",
			"[[Category:PD-Art (PD-old-100)]]",
			"[[Category:PD-Art (PD-old-50)]]",
			"[[Category:PD-Art (PD-old-70)]]",
			"[[Category:PD-Art (PD-old-75)]]",
			"[[Category:PD-Art (PD-old-80)]]",
			"[[Category:PD-Art (PD-old-90)]]",
			"[[Category:PD-Art (PD-old-95)]]",
			"[[Category:PD-Art (PD-old default)]]",
			"[[Category:PD-Art (PD-old-60-expired)]]",
		};

		public PdArtReplacement()
		{
			Directory.CreateDirectory(ProjectDataDirectory);
			m_dateMapping = new ManualMapping<MappingDate>(DateMappingFile);

			m_filesDatabase = ConnectFilesDatabase(true);
		}

		public static SQLiteConnection ConnectFilesDatabase(bool bWantsWrite)
		{
			SQLiteConnectionStringBuilder connectionString = new SQLiteConnectionStringBuilder
			{
				{ "Data Source", FilesDatabaseFile },
				{ "Mode", bWantsWrite ? "ReadWrite" : "ReadOnly" }
			};
			SQLiteConnection connection = new SQLiteConnection(connectionString.ConnectionString);
			connection.Open();
			return connection;
		}

		public override void SaveOut()
		{
			base.SaveOut();

			string statsText = string.Format(
@"RATE: {0}/{1}
InfoFindFail: {2}
DatePrseFail: {3}
NotPDUS:      {4}
NoCreator:    {5}
NoDeathYear:  {6}
PMAFail:      {7}
OtherLicense: {8}",
				qtySuccess, qtyProcessed,
				qtyInfoFindFail, qtyDateParseFail, qtyNotPDUS, qtyNoCreator, qtyNoDeathYear, qtyInsufficientPMA, qtyOtherLicense);
			File.WriteAllText(Path.Combine(ProjectDataDirectory, "stats.txt"), statsText, Encoding.UTF8);

			m_dateMapping.Serialize();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="nakedTemplate">The template without {{}}</param>
		/// <remarks>Assumes the text is actually a PD-Art template.</remarks>
		public static IEnumerable<string> BreakPdArtComponent(string nakedTemplate)
		{
			return nakedTemplate.Split('|').Select(rawComponent =>
			{
				string component = rawComponent.Trim();
				if (component.StartsWith("1=") || component.StartsWith("2="))
				{
					component = component.Substring(2).Trim();
				}
				return component;
			});
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="nakedTemplate">The template without {{}}</param>
		/// <remarks>Assumes the text is actually a PD-Art template.</remarks>
		public bool IsReplaceablePdArt(string nakedTemplate)
		{
			IEnumerable<string> pdArtComponents = BreakPdArtComponent(nakedTemplate);

			// make sure we can dismiss all the parameters
			foreach (string component in pdArtComponents.Skip(1))
			{
				//TODO:
				//if (componentIndex == 2 && pdArtComponents.First().Equals("licensed-pd-art", StringComparison.InvariantCultureIgnoreCase))
				//{
				//	// the last license of {{licensed-pd-art}} can be anything, we will retain it
				//	licensedPdArtOtherLicense = pdArtComponents[componentIndex];
				//	continue;
				//}

				if (!string.IsNullOrEmpty(component)
					&& !s_supersedeLicenses.Contains(component, StringComparer.InvariantCultureIgnoreCase)
					&& !component.StartsWith("deathyear=", StringComparison.InvariantCultureIgnoreCase)
					&& !component.StartsWith("deathdate=", StringComparison.InvariantCultureIgnoreCase)
					&& !component.Equals("country=", StringComparison.InvariantCultureIgnoreCase)
					&& !component.Equals("country=US", StringComparison.InvariantCultureIgnoreCase)
					&& !component.Equals("country=United States", StringComparison.InvariantCultureIgnoreCase))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Returns true if any of the template parameters contain a PMA-bearing license.
		/// </summary>
		/// <param name="nakedTemplate">The template without {{}}</param>
		public bool HasPMALicense(string nakedTemplate)
		{
			foreach (string component in BreakPdArtComponent(nakedTemplate))
			{
				if (s_pmaLicenses.Contains(component, StringComparer.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Determines the license that can replace PD-Art and replaces it.
		/// </summary>
		/// <returns>True if a replacement was made.</returns>
		public override bool DoReplacement(Article article)
		{
			qtyProcessed++;

			if (article.missing)
			{
				RemoveFromCache(article.title);
				return false;
			}

			PageTitle articleTitle = PageTitle.Parse(article.title);
			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (SkipCached && IsFileCached(m_filesDatabase, articleTitle))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Already cached.");
				return false;
			}

			//TODO: check pd-art is already acceptably replaced

			// locate pd-art template(s)
			List<StringSpan> pdArts = new List<StringSpan>();
			foreach (string template in s_pdArtTemplates)
			{
				int workingIndex = 0;
				do
				{
					StringSpan span = WikiUtils.GetTemplateLocation(worksheet.Text, template, workingIndex);
					if (span.IsValid)
					{
						pdArts.Add(span);
						workingIndex = span.end + 1;
					}
					else
					{
						break;
					}
				} while (true);
			}

			string logLicense = null;
			string logInnerLicense = null;
			string[] logComponents = null;
			if (pdArts.Count > 0)
			{
				logLicense = worksheet.Text.Substring(pdArts[0].start, pdArts[0].Length);
				// break pd-art template params
				logComponents = WikiUtils.TrimTemplate(logLicense).Split('|').Select(component => component.Trim()).ToArray();
				if (logComponents.Length >= 2)
				{
					logInnerLicense = logComponents[1];
				}
			}

			// 1. need author death date
			int creatorDeathYear = 9999;

			DateParseMetadata dateParseMetadata = ParseDate(worksheet.Date);

			CacheFile(articleTitle, worksheet.Author, worksheet.Date, dateParseMetadata.LatestYear, creatorDeathYear, logLicense, logInnerLicense);

			if (pdArts.Count == 0)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to find any PD-Art templates.");
				SetLicenseReplaced(articleTitle, ReplacementStatus.NotFound);
				return false;
			}

			string licensedPdArtOtherLicense = null;
			bool bAlreadyHasPMA = false;

			foreach (StringSpan match in pdArts)
			{
				// check for unreplaceable templates
				string nakedTemplate = WikiUtils.TrimTemplate(worksheet.Text.Substring(match.start, match.Length));
				if (!IsReplaceablePdArt(nakedTemplate))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  Can't replace '{0}'.", nakedTemplate);
					SetLicenseReplaced(articleTitle, ReplacementStatus.NotFound); //TODO: log instead
					return false;
				}

				// does it already have exactly one good pd-art template?
				//TODO: remove extraneous templates
				if (s_goodPdArtNakedRegex.IsMatch(nakedTemplate))
				{
					ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "  PD-Art is already replaced.");
					Console.ResetColor();
					SetLicenseReplaced(articleTitle, ReplacementStatus.Replaced);
					return false;
				}

				// is there already a PMA license in here?
				if (!bAlreadyHasPMA)
				{
					bAlreadyHasPMA = HasPMALicense(nakedTemplate);
				}
			}

			SetLicenseReplaced(articleTitle, ReplacementStatus.NotReplaced);

			// A. does wikidata item have author info?
			if (!SkipAuthorLookup && !string.IsNullOrEmpty(worksheet.Wikidata))
			{
				try
				{
					Entity artworkEntity = GlobalAPIs.Wikidata.GetEntity(worksheet.Wikidata);
					if (artworkEntity != null)
					{
						var artistEntities = artworkEntity.GetClaimValuesAsEntities(Wikidata.Prop_Creator, GlobalAPIs.Wikidata);
						if (artistEntities.Any())
						{
							creatorDeathYear = artistEntities
								.Select(e =>
								{
									IEnumerable<MediaWiki.DateTime> deathTimes = e.GetClaimValuesAsDates(Wikidata.Prop_DateOfDeath)
										.Where(date => date != null && date.Precision >= MediaWiki.DateTime.YearPrecision);
									if (deathTimes.Any())
									{
										return deathTimes.Max(date => date.GetYear());
									}
									else
									{
										return 9999;
									}
								}).Max();
						}
					}
				}
				catch (WikimediaCodeException e)
				{
					if (e.Code != "no-such-entity")
					{
						throw;
					}
				}
			}

			if (creatorDeathYear == 9999)
			{
				if (string.IsNullOrEmpty(worksheet.Author))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to find author.");
					return false;
				}

				if (ImplicitCreatorsReplacement.IsUnknownAuthor(worksheet.Author)
					|| ImplicitCreatorsReplacement.IsAnonymousAuthor(worksheet.Author))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  Anonymous/unknown author.");
					qtyInfoFindFail++;
					return false;
				}

				// B. is author a creator?
				PageTitle literalCreator = CreatorUtility.GetCreatorTemplate(worksheet.Author);
				if (!literalCreator.IsEmpty)
				{
					Creator creator = CreatorUtility.GetCreator("{{" + literalCreator + "}}");
					creator.Usage++;
					creatorDeathYear = creator.DeathYear;
				}
				else if (ImplicitCreatorsReplacement.SlowCategoryWalk)
				{
					// C. can author be associated to a creator based on file categories?
					PageTitle categoryCreator = ImplicitCreatorsReplacement.GetCreatorFromCategories(worksheet.Author, WikiUtils.GetCategories(worksheet.Text), 1);
					Creator creator = CreatorUtility.GetCreator("{{" + categoryCreator + "}}");
					creator.Usage++;
					creatorDeathYear = creator.DeathYear;
				}
			}

			if (creatorDeathYear == 9999)
			{
				// D. does author string contain a death date?
				Match match = CreatorUtility.AuthorLifespanRegex.Match(worksheet.Author);
				if (match.Success)
				{
					creatorDeathYear = int.Parse(match.Groups[3].Value);
				}
			}

			if (creatorDeathYear == 9999)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Can't determine death year for creator '{0}'.", worksheet.Author);
				qtyNoDeathYear++;
				return false;
			}

			CacheFile(articleTitle, worksheet.Author, worksheet.Date, dateParseMetadata.LatestYear, creatorDeathYear, logLicense, logInnerLicense);

			// If the license already has some kind of PMA, go ahead and replace it regardless of the duration
			int pmaYear = System.DateTime.Now.Year - 100;
			if (creatorDeathYear >= pmaYear && !bAlreadyHasPMA)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Death year {0} is inside max PMA.", creatorDeathYear);
				qtyInsufficientPMA++;
				return false;
			}

			MappingDate mappedDate = null;

			// 2. try to parse file/pub date
			int latestYear;
			if (string.IsNullOrEmpty(worksheet.Date))
			{
				// if no file/pub date, assume it is not later than the death year
				latestYear = creatorDeathYear;
			}
			else if (dateParseMetadata.LatestYear != 9999)
			{
				latestYear = dateParseMetadata.LatestYear;
			}
			else
			{
				// unparseable date
				mappedDate = m_dateMapping.TryMapValue(worksheet.Date, articleTitle);
				if (!string.IsNullOrEmpty(mappedDate.ReplaceDate))
				{
					// make the date replacement
					//TODO: not safe to modify worksheet.Text here
					//{
					//	Console.ForegroundColor = ConsoleColor.Green;
					//	Console.WriteLine("  Date '{0}' is mapped to '{1}'.", worksheet.Date, mappedDate.ReplaceDate);
					//	Console.ResetColor();
					//
					//	string textBefore = worksheet.Text.Substring(0, worksheet.DateIndex);
					//	string textAfter = worksheet.Text.Substring(worksheet.DateIndex + worksheet.Date.Length);
					//	worksheet.Text = textBefore + mappedDate.ReplaceDate + textAfter;
					//}

					dateParseMetadata = ParseDate(mappedDate.ReplaceDate);
					if (dateParseMetadata.LatestYear != 9999)
					{
						latestYear = dateParseMetadata.LatestYear;
					}
					else if (mappedDate.LatestYear != 9999)
					{
						latestYear = mappedDate.LatestYear;
					}
					else
					{
						ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to parse mapped date '{0}'.", worksheet.Date);
						qtyDateParseFail++;
						return false;
					}
				}
				else if (mappedDate.LatestYear != 9999)
				{
					latestYear = mappedDate.LatestYear;
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to parse date '{0}'.", worksheet.Date);
					qtyDateParseFail++;
					return false;
				}
			}

			// Is file/pub date expired in the US?
			// Exception: post-2004 dates are very likely to be upload dates instead of pub dates, especially given that the author died at least 100 years ago.
			if (latestYear >= System.DateTime.Now.Year - 95 && latestYear < 2004)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Date {0} is after the US expired threshold.", latestYear);
				qtyNotPDUS++;
				return false;
			}

			string newLicense;
			if (!string.IsNullOrEmpty(licensedPdArtOtherLicense))
			{
				newLicense = string.Format("{{{{Licensed-PD-Art|PD-old-auto-expired|deathyear={0}|{1}}}}}", creatorDeathYear, licensedPdArtOtherLicense);
			}
			else
			{
				newLicense = string.Format("{{{{PD-Art|PD-old-auto-expired|deathyear={0}}}}}", creatorDeathYear);
			}

			ConsoleUtility.WriteLine(ConsoleColor.Green, "  Replacing PD-Art with '{0}'.", newLicense);

			qtySuccess++;

			// replace the LAST PD-Art template
			worksheet.Text = worksheet.Text.Substring(0, pdArts.Last().start)
				+ newLicense
				+ worksheet.Text.Substring(pdArts.Last().end + 1);

			// remove extraneous PD-Art templates
			for (int rangeIndex = pdArts.Count - 2; rangeIndex >= 0; --rangeIndex)
			{
				worksheet.Text = WikiUtils.RemoveTemplate(worksheet.Text, pdArts[rangeIndex]);
			}

			// remove PD licenses that will be completely expressed by the new license
			foreach (string supersededLicense in s_supersedeLicenses)
			{
				string removedTemplate;
				worksheet.Text = WikiUtils.RemoveTemplate(supersededLicense, worksheet.Text, out removedTemplate);
				if (!string.IsNullOrEmpty(removedTemplate))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Green, "  Removed '{0}'.", removedTemplate.Trim());
				}
			}

			// Other licenses will be reported as conflicts
			foreach (string license in LicenseUtility.PrimaryLicenseTemplates)
			{
				// CC licenses are fine (probably a back-up license from the photographer)
				if (license.StartsWith("cc-", StringComparison.InvariantCultureIgnoreCase))
				{
					//TODO: use Licensed-PD-Art
					continue;
				}

				if (WikiUtils.HasTemplate(worksheet.Text, license))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  Contains other license '{0}'.", license);
					qtyOtherLicense++;
					File.AppendAllText(DuplicateLicensesLogFile, article.title + "\n");
					return false;
				}
			}

			article.Dirty = true;
			article.Changes.Add("improving PD-art license with more information based on file data");

			SetLicenseReplaced(articleTitle, ReplacementStatus.Replaced);

			// remove date mapping
			if (mappedDate != null)
			{
				mappedDate.FromPages.Remove(article.title);
				m_dateMapping.SetDirty();
			}

			return true;
		}

		private void CacheFile(PageTitle title, string author, string date, int? latestYear, int deathyear, string pdArtLicense, string innerLicense)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();

			// do not replace a cached author deathyear with a junk one
			if (deathyear == 9999)
			{
				command.CommandText = "INSERT INTO files (pageTitle, authorString, dateString, latestYear, authorDeathYear, pdArtLicense, innerLicense) "
					+ "VALUES ($pageTitle, $authorString, $dateString, $latestYear, $authorDeathYear, $pdArtLicense, $innerLicense) "
					+ "ON CONFLICT (pageTitle) DO UPDATE "
					+ "SET authorString=$authorString, dateString=$dateString, pdArtLicense=$pdArtLicense, innerLicense=$innerLicense";
			}
			else
			{
				command.CommandText = "INSERT INTO files (pageTitle, authorString, dateString, latestYear, authorDeathYear, pdArtLicense, innerLicense) "
					+ "VALUES ($pageTitle, $authorString, $dateString, $latestYear, $authorDeathYear, $pdArtLicense, $innerLicense) "
					+ "ON CONFLICT (pageTitle) DO UPDATE "
					+ "SET authorString=$authorString, dateString=$dateString, latestYear=$latestYear, authorDeathYear=$authorDeathYear, pdArtLicense=$pdArtLicense, innerLicense=$innerLicense";
			}
			
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorString", author);
			command.Parameters.AddWithValue("dateString", date);
			command.Parameters.AddWithValue("latestYear", latestYear);
			command.Parameters.AddWithValue("authorDeathYear", deathyear);
			command.Parameters.AddWithValue("pdArtLicense", pdArtLicense);
			command.Parameters.AddWithValue("innerLicense", innerLicense);
			Debug.Assert(command.ExecuteNonQuery() == 1);
		}

		public void RemoveFromCache(string title)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "DELETE FROM files WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			Debug.Assert(command.ExecuteNonQuery() == 1);
		}

		public static bool IsFileCached(SQLiteConnection connection, PageTitle title)
		{
			SQLiteCommand command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(*) FROM files WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			using (var reader = command.ExecuteReader())
			{
				reader.Read();
				return reader.GetInt32(0) > 0;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="title"></param>
		private void SetLicenseReplaced(PageTitle title, ReplacementStatus state)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET bLicenseReplaced=$state WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("state", (int)state);
			command.ExecuteNonQuery();
		}

		private static bool IsAllowedOtherDateClass(string dateClass)
		{
			return dateClass == "-"
				|| dateClass == "between"
				|| dateClass == "~"
				|| dateClass == "ca"
				|| dateClass == "circa"
				|| dateClass == "c"
				|| dateClass == "or"
				|| dateClass == "and"
				|| dateClass == "&"
				|| dateClass == "<"
				|| dateClass == "b"
				|| dateClass == "before"
				|| dateClass == ">"
				|| dateClass == "a"
				|| dateClass == "after"
				|| dateClass == "by"
				|| dateClass == "until"
				|| dateClass == "summer"
				|| dateClass == "winter"
				|| dateClass == "fall"
				|| dateClass == "autumn"
				|| dateClass == "spring"
				;
		}

		public static DateParseMetadata ParseDate(string date)
		{
			date = date.Trim();

			// check for "other date" template
			StringSpan otherDateSpan = WikiUtils.GetTemplateLocation(date, "other date");
			if (!otherDateSpan.IsValid)
			{
				otherDateSpan = WikiUtils.GetTemplateLocation(date, "otherdate");
			}
			if (otherDateSpan.IsValid)
			{
				string otherDate = date.Substring(otherDateSpan);
				string dateClass = WikiUtils.GetTemplateParameter(1, otherDate);
				if (IsAllowedOtherDateClass(dateClass))
				{
					string date1 = WikiUtils.GetTemplateParameter(2, otherDate);
					string date2 = WikiUtils.GetTemplateParameter(3, otherDate);
					if (!string.IsNullOrEmpty(date2))
					{
						return DateParseMetadata.Combine(ParseDate(date1), ParseDate(date2));
					}
					else
					{
						return ParseDate(date1);
					}
				}
				else if (dateClass == "century")
				{
					string century = WikiUtils.GetTemplateParameter(2, otherDate);
					if (int.TryParse(century, out int centuryInt))
					{
						return new DateParseMetadata(0, (centuryInt - 1) * 100, centuryInt * 100 - 1);
					}
				}
				else if (dateClass == "decade")
				{
					string decade = WikiUtils.GetTemplateParameter(2, otherDate);
					if (int.TryParse(decade, out int decadeInt))
					{
						return new DateParseMetadata(0, decadeInt, decadeInt + 9);
					}
				}
				else if (dateClass == "s")
				{
					string period = WikiUtils.GetTemplateParameter(2, otherDate);
					return ParsePeriod(period);
				}
			}

			// check for "circa" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "circa");
				if (span.IsValid)
				{
					string circa = date.Substring(span);
					string circaDate = WikiUtils.GetTemplateParameter(1, circa);
					return ParseDate(circaDate);
				}
			}

			// check for "between" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "between");
				if (span.IsValid)
				{
					string between = date.Substring(span);
					string date1 = WikiUtils.GetTemplateParameter(1, between);
					string date2 = WikiUtils.GetTemplateParameter(2, between);
					return DateParseMetadata.Combine(ParseDate(date1), ParseDate(date2));
				}
			}

			// check for "before" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "before");
				if (span.IsValid)
				{
					string before = date.Substring(span);
					string date1 = WikiUtils.GetTemplateParameter(1, before);
					return ParseDate(date1);
				}
			}

			// check for "date" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "date");
				if (span.IsValid)
				{
					string dateTemplate = date.Substring(span);
					string date1 = WikiUtils.GetTemplateParameter(1, dateTemplate);
					return ParseDate(date1);
				}
			}

			// check for "year" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "year");
				if (span.IsValid)
				{
					string yearTemplate = date.Substring(span);
					string year1 = WikiUtils.GetTemplateParameter(1, yearTemplate);
					return ParseDate(year1);
				}
			}

			// check for "original upload date" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "original upload date");
				if (span.IsValid)
				{
					string dateTemplate = date.Substring(span);
					string date1 = WikiUtils.GetTemplateParameter(1, dateTemplate);
					return ParseDate(date1);
				}
			}

			// check for "date context" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "date context");
				if (span.IsValid)
				{
					string dateTemplate = date.Substring(span);
					string context = WikiUtils.GetTemplateParameter(1, dateTemplate);
					string date1 = WikiUtils.GetTemplateParameter(2, dateTemplate);
					if (context == "created" || context == "published")
					{
						return ParseDate(date1);
					}
				}
			}

			// check for "complex date" template
			{
				//TODO:
			}

			// explicit strings
			if (date.Equals("Edo Period", StringComparison.InvariantCultureIgnoreCase))
			{
				return new DateParseMetadata(9999, 1603, 1868);
			}

			// year regexes
			foreach (Regex regex in s_dateRegexes)
			{
				Match match = regex.Match(date);
				if (match.Success)
				{
					DateParseMetadata? metadata = null;
					for (int g = 1; g < match.Groups.Count; ++g)
					{
						DateParseMetadata groupMetadata = ParseDate(match.Groups[g].Value);
						if (metadata == null)
						{
							metadata = groupMetadata;
						}
						else
						{
							metadata = DateParseMetadata.Combine(metadata.Value, groupMetadata);
						}
					}
					return metadata.Value;
				}
			}

			// century regexes
			foreach (Regex regex in s_centuryRegexes)
			{
				Match match = regex.Match(date);
				if (match.Success)
				{
					DateParseMetadata? metadata = null;
					for (int g = 1; g < match.Groups.Count; ++g)
					{
						int century = int.Parse(match.Groups[g].Value);
						DateParseMetadata groupMetadata = new DateParseMetadata();
						groupMetadata.EarliestYear = century * 100 - 100;
						groupMetadata.LatestYear = century * 100 - 1;
						if (metadata == null)
						{
							metadata = groupMetadata;
						}
						else
						{
							metadata = DateParseMetadata.Combine(metadata.Value, groupMetadata);
						}
					}
					return metadata.Value;
				}
			}

			date = date.Trim();
			date = date.Trim('[', ']', '(', ')');

			// check for '0000s'
			{
				Match match = s_sRegex.Match(date);
				if (match.Success)
				{
					return ParsePeriod(match.Groups[1].Value);
				}
			}

			// check for a date C# can parse
			if (System.DateTime.TryParse(date, out System.DateTime parsedDate))
			{
				return new DateParseMetadata(parsedDate.Year);
			}

			// check for '0000'
			if (int.TryParse(date, out int parsedYear) && parsedYear < System.DateTime.Now.Year && parsedYear > 100)
			{
				return new DateParseMetadata(parsedYear);
			}

			// check for '0000-0000' (TODO: regex)
			{
				string[] dateSplit = date.Split('-', '–');
				if (dateSplit.Length == 2 && int.TryParse(dateSplit[0].Trim(), out int date1) && int.TryParse(dateSplit[1].Trim(), out int date2))
				{
					if (date1 > 100)
					{
						if (date2 > 100)
						{
							return new DateParseMetadata(0, date1, date2);
						}
						else
						{
							// e.g. 1850-55
							int length2 = dateSplit[1].Length;
							int magnitude = (int)Math.Pow(10, length2);
							date2 = magnitude * (date1 / magnitude) + date2;
							return new DateParseMetadata(0, date1, date2);
						}
					}
				}
			}

			return DateParseMetadata.Unknown;
		}

		private static DateParseMetadata ParsePeriod(string period)
		{
			int imprecision = 0;
			for (int i = period.Length - 1; i >= 0; --i)
			{
				if (period[i] == '0')
				{
					imprecision++;
				}
				else
				{
					break;
				}
			}
			if (int.TryParse(period, out int periodInt))
			{
				return new DateParseMetadata(0, periodInt, periodInt + 10 ^ imprecision - 1);
			}
			else
			{
				return DateParseMetadata.Unknown;
			}
		}
	}
}
