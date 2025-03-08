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

		public static string NoPdArtFile
		{
			get { return Path.Combine(ProjectDataDirectory, "no-pd-art.txt"); }
		}

		public static string DateMappingFile
		{
			get { return Path.Combine(ProjectDataDirectory, "date-mappings.txt"); }
		}

		private static readonly Regex s_PdArtRaw = new Regex(@"{{[Pp][Dd]\-[Aa]rt(\-[0-9]+)?(?:\-[Tt]wo)?\s*}}");
		private static readonly Regex s_PdArtPdOld = new Regex(@"{{[Pp][Dd]\-[Aa]rt\s*\|(?:1=)?([Pp][Dd]\-[Oo]ld\-[0-9]+)\s*}}");
		private static readonly Regex[] s_PdArtForms = new Regex[] { s_PdArtRaw, s_PdArtPdOld };

		private static readonly string[] s_supersedeLicenses = new string[]
		{
			"PD-old",
			"PD-old-100",
			"PD-old-100-1964",
			"PD-old-100-1996",
			"PD-old-100-expired",
			"PD-old-50",
			"PD-old-50-1964",
			"PD-old-50-1996",
			"PD-old-50-expired",
			"PD-old-60-1964",
			"PD-old-60-1996",
			"PD-old-60-expired",
			"PD-old-70",
			"PD-old-70-1964",
			"PD-old-70-1996",
			"PD-old-70-expired",
			"PD-old-75",
			"PD-old-75-1964",
			"PD-old-75-1996",
			"PD-old-75-expired",
			"PD-old-80",
			"PD-old-80-1964",
			"PD-old-80-1996",
			"PD-old-80-expired",
			"PD-old-90",
			"PD-old-90-expired",
			"PD-old-95",
			"PD-old-95-1964",
			"PD-old-95-1996",
			"PD-old-95-expired",
			"PD-old-assumed",
			"PD-old-assumed-expired",
			"PD-old-assumed/sandbox",
			"PD-old-auto-1964",
			"PD-old-auto-1996",
			"PD-old-auto-expired",
			"PD-US",
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
		/// Determines the license that can replace PD-Art and replaces it.
		/// </summary>
		/// <returns>True if a replacement was made.</returns>
		public override bool DoReplacement(Article article)
		{
			qtyProcessed++;

			PageTitle articleTitle = PageTitle.Parse(article.title);
			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (SkipCached && IsFileCached(m_filesDatabase, articleTitle))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("  Already cached.");
				Console.ResetColor();
				return false;
			}

			Match pdArtRawMatch = s_PdArtRaw.Match(worksheet.Text);
			Match pdArtPdOldMatch = s_PdArtPdOld.Match(worksheet.Text);

			string pdArtLicense = "";
			if (pdArtPdOldMatch.Success)
			{
				pdArtLicense = pdArtPdOldMatch.Groups[0].Value;
			}

			// 1. need author death date
			int creatorDeathYear = 9999;

			CacheFile(articleTitle, worksheet.Author, worksheet.Date, creatorDeathYear, pdArtLicense);

			if (!pdArtRawMatch.Success && !pdArtPdOldMatch.Success)
			{
				if (worksheet.Text.Contains("{{PD-Art-YorckProject}}"))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("  PD-Art-YorckProject");
					Console.ResetColor();
					return false;
				}
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Failed to find PD-Art template.");
				Console.ResetColor();
				SetLicenseReplaced(articleTitle, 2);
				File.AppendAllText(NoPdArtFile, article.title + "\n", Encoding.UTF8);
				return false;
			}

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
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("  Failed to find author.");
					Console.ResetColor();
					return false;
				}

				if (ImplicitCreatorsReplacement.IsUnknownAuthor(worksheet.Author)
					|| ImplicitCreatorsReplacement.IsAnonymousAuthor(worksheet.Author))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("  Anonymous/unknown author.");
					Console.ResetColor();
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
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Can't determine death year for creator '{0}'.", worksheet.Author);
				Console.ResetColor();
				qtyNoDeathYear++;
				return false;
			}

			CacheFile(articleTitle, worksheet.Author, worksheet.Date, creatorDeathYear, pdArtLicense);

			int pmaYear = System.DateTime.Now.Year - 100;
			if (creatorDeathYear >= pmaYear)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Death year {0} is inside max PMA.", creatorDeathYear);
				Console.ResetColor();
				qtyInsufficientPMA++;
				return false;
			}

			MappingDate mappedDate = null;

			// 2. try to parse file/pub date
			DateParseMetadata dateParseMetadata = ParseDate(worksheet.Date);
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
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("  Date '{0}' is mapped to '{1}'.", worksheet.Date, mappedDate.ReplaceDate);
						Console.ResetColor();

						string textBefore = worksheet.Text.Substring(0, worksheet.DateIndex);
						string textAfter = worksheet.Text.Substring(worksheet.DateIndex + worksheet.Date.Length);
						worksheet.Text = textBefore + mappedDate.ReplaceDate + textAfter;
					}

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
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("  Failed to parse mapped date '{0}'.", worksheet.Date);
						Console.ResetColor();
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
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("  Failed to parse date '{0}'.", worksheet.Date);
					Console.ResetColor();
					qtyDateParseFail++;
					return false;
				}
			}

			// Is file/pub date expired in the US?
			// Exception: post-2004 dates are very likely to be upload dates instead of pub dates, especially given that the author died at least 100 years ago.
			if (latestYear >= System.DateTime.Now.Year - 95 && latestYear < 2004)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Date {0} is after the US expired threshold.", latestYear);
				Console.ResetColor();
				qtyNotPDUS++;
				return false;
			}

			// PD licenses that will be completely expressed by the new license can be removed
			foreach (string supersededLicense in s_supersedeLicenses)
			{
				string removedTemplate;
				worksheet.Text = WikiUtils.RemoveTemplate(supersededLicense, worksheet.Text, out removedTemplate);
				if (!string.IsNullOrEmpty(removedTemplate))
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("  Removed '{0}'.", removedTemplate);
					Console.ResetColor();
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

				if (license.Equals("PD-Art-YorckProject"))
				{
					continue;
				}

				if (WikiUtils.HasTemplate(worksheet.Text, license))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("  Contains other license '{0}'.", license);
					Console.ResetColor();
					qtyOtherLicense++;
					File.AppendAllText(DuplicateLicensesLogFile, article.title + "\n");
					return false;
				}
			}

			string newLicense = string.Format("{{{{PD-Art|PD-old-auto-expired|deathyear={0}}}}}", creatorDeathYear);

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("  Replacing PD-Art with '{0}'.", newLicense);
			Console.ResetColor();

			qtySuccess++;

			//TODO: handle multiple PD-arts of same form
			foreach (Regex pdArtRegex in s_PdArtForms)
			{
				Match match = pdArtRegex.Match(worksheet.Text);
				if (match.Success)
				{
					worksheet.Text = worksheet.Text.SubstringRange(0, match.Index - 1)
						+ newLicense
						+ worksheet.Text.SubstringRange(match.Index + match.Length, worksheet.Text.Length - 1);
					article.Dirty = true;
					article.Changes.Add("replacing PD-art with a more accurate license based on file data");
				}
			}

			SetLicenseReplaced(articleTitle, 1);

			// remove date mapping
			if (mappedDate != null)
			{
				mappedDate.FromPages.Remove(article.title);
				m_dateMapping.SetDirty();
			}

			return true;
		}

		private void CacheFile(PageTitle title, string author, string date, int deathyear, string pdArtLicense)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "INSERT INTO files (pageTitle, authorString, dateString, authorDeathYear, pdArtLicense) "
				+ "VALUES ($pageTitle, $authorString, $dateString, $authorDeathYear, $pdArtLicense) "
				+ "ON CONFLICT (pageTitle) DO UPDATE "
				+ "SET authorString=$authorString, dateString=$dateString, authorDeathYear=$authorDeathYear, pdArtLicense=$pdArtLicense";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorString", author);
			command.Parameters.AddWithValue("dateString", date);
			command.Parameters.AddWithValue("authorDeathYear", deathyear);
			command.Parameters.AddWithValue("pdArtLicense", pdArtLicense);
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
		/// <param name="state">0 = not replaced, 1 = PD-Art replaced, 2 = PD-Art not found, 3 = either 1 or 2 (unknown)</param>
		private void SetLicenseReplaced(PageTitle title, int state)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET bLicenseReplaced=$state WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("state", state);
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
			WikiUtils.GetTemplateLocation(date, "other date", out int otherDateStart, out int otherDateEnd);
			if (otherDateStart < 0)
			{
				WikiUtils.GetTemplateLocation(date, "otherdate", out otherDateStart, out otherDateEnd);
			}
			if (otherDateStart >= 0)
			{
				string otherDate = date.Substring(otherDateStart, otherDateEnd - otherDateStart + 1);
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
				WikiUtils.GetTemplateLocation(date, "circa", out int circaStart, out int circaEnd);
				if (circaStart >= 0)
				{
					string circa = date.Substring(circaStart, circaEnd - circaStart + 1);
					string circaDate = WikiUtils.GetTemplateParameter(1, circa);
					return ParseDate(circaDate);
				}
			}

			// check for "between" template
			{
				WikiUtils.GetTemplateLocation(date, "between", out int betweenStart, out int betweenEnd);
				if (betweenStart >= 0)
				{
					string between = date.Substring(betweenStart, betweenEnd - betweenStart + 1);
					string date1 = WikiUtils.GetTemplateParameter(1, between);
					string date2 = WikiUtils.GetTemplateParameter(2, between);
					return DateParseMetadata.Combine(ParseDate(date1), ParseDate(date2));
				}
			}

			// check for "before" template
			{
				WikiUtils.GetTemplateLocation(date, "before", out int beforeStart, out int beforeEnd);
				if (beforeStart >= 0)
				{
					string before = date.Substring(beforeStart, beforeEnd - beforeStart + 1);
					string date1 = WikiUtils.GetTemplateParameter(1, before);
					return ParseDate(date1);
				}
			}

			// check for "date" template
			{
				WikiUtils.GetTemplateLocation(date, "date", out int dateStart, out int dateEnd);
				if (dateStart >= 0)
				{
					string dateTemplate = date.Substring(dateStart, dateEnd - dateStart + 1);
					string date1 = WikiUtils.GetTemplateParameter(1, dateTemplate);
					return ParseDate(date1);
				}
			}

			// check for "year" template
			{
				WikiUtils.GetTemplateLocation(date, "year", out int yearStart, out int yearEnd);
				if (yearStart >= 0)
				{
					string yearTemplate = date.Substring(yearStart, yearEnd - yearStart + 1);
					string year1 = WikiUtils.GetTemplateParameter(1, yearTemplate);
					return ParseDate(year1);
				}
			}

			// check for "original upload date" template
			{
				WikiUtils.GetTemplateLocation(date, "original upload date", out int dateStart, out int dateEnd);
				if (dateStart >= 0)
				{
					string dateTemplate = date.Substring(dateStart, dateEnd - dateStart + 1);
					string date1 = WikiUtils.GetTemplateParameter(1, dateTemplate);
					return ParseDate(date1);
				}
			}

			// check for "date context" template
			{
				WikiUtils.GetTemplateLocation(date, "date context", out int dateStart, out int dateEnd);
				if (dateStart >= 0)
				{
					string dateTemplate = date.Substring(dateStart, dateEnd - dateStart + 1);
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
