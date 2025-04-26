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

namespace Tasks.Commons
{
	/// <summary>
	/// Replaces PD-Art tags with imprecise licenses with a more specific license, if one can be determined.
	/// </summary>
	/// <remarks>
	/// Database Unix Seconds for changes:
	/// 1744582780 Caching with no early-outs was implemented.
	/// 1745684896 Artwork wikidata is now being looked up and used for latestYear
	/// </remarks>
	public class PdArtReplacement : BaseReplacement
	{
		public enum ReplacementStatus
		{
			NotReplaced = 0,
			Replaced = 1,
			NotFound = 2,
		}

		public override bool UseHeartbeat
		{
			get { return true; }
		}

		/// <summary>
		/// If set, skips files that are already cached.
		/// </summary>
		public static bool SkipCached = true;

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
		private ManualMapping<MappingCreator> m_creatorMappings;

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
			"PD-old-1923", //TODO: rerun me!
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
			m_creatorMappings = new ManualMapping<MappingCreator>(ImplicitCreatorsReplacement.GetCreatorMappingFile(ProjectDataDirectory));

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

		private List<StringSpan> GetPdArtTemplates(string text)
		{
			List<StringSpan> pdArts = new List<StringSpan>();
			foreach (string template in s_pdArtTemplates)
			{
				int workingIndex = 0;
				do
				{
					StringSpan span = WikiUtils.GetTemplateLocation(text, template, workingIndex);
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
			return pdArts;
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

			if (SkipCached && IsFileCached(m_filesDatabase, articleTitle))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Already cached.");
				return false;
			}

			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			// any errors that prevent the replacement from being made
			List<string> errors = new List<string>();

			ReplacementStatus replacementStatus = ReplacementStatus.NotReplaced;

			// locate pd-art template(s)
			List<StringSpan> pdArts = GetPdArtTemplates(worksheet.Text);

			if (pdArts.Count == 0)
			{
				errors.Add("Failed to find any PD-Art templates.");
				replacementStatus = ReplacementStatus.NotFound;
			}

			{
				string logLicense = null;
				string logInnerLicense = null;
				if (pdArts.Count > 0)
				{
					logLicense = worksheet.Text.Substring(pdArts[0].start, pdArts[0].Length);
					// break pd-art template params
					string[] logComponents = WikiUtils.TrimTemplate(logLicense).Split('|').Select(component => component.Trim()).ToArray();
					if (logComponents.Length >= 2)
					{
						logInnerLicense = logComponents[1];
					}
				}

				CacheFile(articleTitle, worksheet.Author, worksheet.Date, logLicense, logInnerLicense);
			}

			// if the file uses Licensed-PD-Art, the other (non-PD) license
			//TODO: reimplement me
			string licensedPdArtOtherLicense = null;

			// does the license template already have any PMA-bearing license?
			bool bAlreadyHasPMA = false;

			bool bLicenseAlreadyReplaced = false;

			foreach (StringSpan match in pdArts)
			{
				// check for unreplaceable templates
				string nakedTemplate = WikiUtils.TrimTemplate(worksheet.Text.Substring(match.start, match.Length));

				if (!IsReplaceablePdArt(nakedTemplate))
				{
					errors.Add(string.Format("Can't replace '{0}'.", nakedTemplate));
					replacementStatus = ReplacementStatus.NotFound; //TODO: log instead
				}
				else if (s_goodPdArtNakedRegex.IsMatch(nakedTemplate)
					|| nakedTemplate.Equals("PD-Art|PD-old-100-expired", StringComparison.InvariantCultureIgnoreCase))
				{
					bLicenseAlreadyReplaced = true;
					CacheNewLicense(articleTitle, "{{" + nakedTemplate + "}}");
				}

				// is there already a PMA license in here?
				//TODO: detect PD-Art-70 etc
				if (!bAlreadyHasPMA)
				{
					bAlreadyHasPMA = HasPMALicense(nakedTemplate);
				}
			}

			CacheReplacementStatus(articleTitle, replacementStatus);
			CacheNewLicense(articleTitle, null);

			// 1. find author death date
			int creatorDeathYear;
			int? creatorCountryOfCitizenship;
			CreatorData creatorData = GetAuthorData(worksheet);
			if (creatorData != null)
			{
				creatorDeathYear = creatorData.DeathYear;
				creatorCountryOfCitizenship = creatorData.CountryOfCitizenship;
			}
			else
			{
				creatorDeathYear = 9999;
				creatorCountryOfCitizenship = null;
			}

			int latestYear = GetLatestPublicationYear(worksheet, out MappingDate mappedDate);

			int pmaDuration = LicenseUtility.GetPMADurationByQID(creatorCountryOfCitizenship);
			Console.WriteLine("  Date: {0}, Deathyear: {1}, PMA: {2}", latestYear, creatorDeathYear, pmaDuration);

			if (latestYear == 9999)
			{
				errors.Add("Failed to identify publication date.");
				qtyDateParseFail++;
			}
			CacheLatestYear(articleTitle, latestYear);

			if (creatorDeathYear == 9999)
			{
				errors.Add("Failed to find author deathyear.");
			}
			else
			{
				CacheDeathyear(articleTitle, creatorDeathYear);
			}

			if (bLicenseAlreadyReplaced)
			{
				//TODO: still remove extraneous templates
				ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "  PD-Art is already replaced.");
				Console.ResetColor();
				CacheReplacementStatus(articleTitle, ReplacementStatus.Replaced);
				return false;
			}

			// is pub date old enough to assume 100 PMA?
			bool bReallyOldUnknownAuthor = ImplicitCreatorsReplacement.IsUnknownAuthor(worksheet.Author) && latestYear < System.DateTime.Now.Year - 175;

			if (creatorDeathYear == 9999)
			{
				if (bReallyOldUnknownAuthor)
				{
					Console.WriteLine("  Unknown author and date older than 175.");
				}
				else
				{
					errors.Add(string.Format("Can't determine death year for creator '{0}'.", worksheet.Author));
					qtyNoDeathYear++;
				}
			}
			else if (creatorDeathYear == 10000)
			{
				Console.WriteLine("  Unknown author deathyear older than 100.");
			}
			else if (creatorDeathYear >= System.DateTime.Now.Year - pmaDuration) 
			{
				if (bAlreadyHasPMA)
				{
					// If the license already has some kind of PMA, go ahead and replace it regardless of the duration
					Console.WriteLine("  PMA too short but license already has one.");
				}
				else
				{
					errors.Add(string.Format("Death year {0} is inside max PMA.", creatorDeathYear));
					qtyInsufficientPMA++;
				}
			}

			// Is file/pub date expired in the US?
			int usExpiredYear = System.DateTime.Now.Year - 95;
			if (latestYear >= usExpiredYear)
			{
				// Exception: post-2004 dates are very likely to be upload dates instead of pub dates
				if ((latestYear != 9999 && latestYear >= 2004) && creatorDeathYear < System.DateTime.Now.Year - 120)
				{
					Console.WriteLine("  Scan/upload date, death year older than 120.");
				}
				else
				{
					errors.Add(string.Format("Date {0} is after the US expired threshold.", latestYear));
					qtyNotPDUS++;
				}
			}

			string changeText;
			string newLicense;
			if (bReallyOldUnknownAuthor || creatorDeathYear == 10000)
			{
				newLicense = string.Format("{{{{PD-Art|PD-old-100-expired}}}}");
				changeText = "improving PD-art license: date older than 175 yrs and author unknown";
			}
			else if (!string.IsNullOrEmpty(licensedPdArtOtherLicense))
			{
				newLicense = string.Format("{{{{Licensed-PD-Art|PD-old-auto-expired|deathyear={0}|{1}}}}}", creatorDeathYear, licensedPdArtOtherLicense);
				changeText = "improving PD-art license with more information based on file data";
			}
			else
			{
				newLicense = string.Format("{{{{PD-Art|PD-old-auto-expired|deathyear={0}}}}}", creatorDeathYear);
				changeText = "improving PD-art license with more information based on file data";
			}

			qtySuccess++;

			List<StringSpan> allReplaceableLicenses = GetPdArtTemplates(worksheet.Text);
			foreach (string supersededLicense in s_supersedeLicenses)
			{
				int currentLocation = 0;
				while (true)
				{
					StringSpan span = WikiUtils.GetTemplateLocation(worksheet.Text, supersededLicense, currentLocation);
					if (span.IsValid)
					{
						allReplaceableLicenses.Add(span);
						currentLocation = span.end + 1;
					}
					else
					{
						break;
					}
				}
			}
			allReplaceableLicenses.Sort((a, b) => a.end - b.end);

			string oldText = worksheet.Text;
			string replacedLicense = "";

			// replace the LAST license template
			if (allReplaceableLicenses.Any())
			{
				replacedLicense = worksheet.Text.Substring(allReplaceableLicenses.Last());
				worksheet.Text = worksheet.Text.Substring(0, allReplaceableLicenses.Last().start)
					+ newLicense
					+ worksheet.Text.Substring(allReplaceableLicenses.Last().end + 1);

				// remove extraneous license templates
				for (int rangeIndex = allReplaceableLicenses.Count - 2; rangeIndex >= 0; --rangeIndex)
				{
					string template = worksheet.Text.Substring(allReplaceableLicenses[rangeIndex]);
					worksheet.Text = WikiUtils.RemoveTemplate(worksheet.Text, allReplaceableLicenses[rangeIndex]);
					ConsoleUtility.WriteLine(ConsoleColor.Green, "  Removed '{0}'.", template.Trim());
				}
			}
			else
			{
				errors.Add("No replaceable licenses.");
			}

			// Other licenses will be reported as conflicts
			string conflictLicenses = "";
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
					errors.Add(string.Format("Contains other license '{0}'.", license));
					conflictLicenses = StringUtility.Join("|", conflictLicenses, license);
				}
			}
			if (!string.IsNullOrEmpty(conflictLicenses))
			{
				qtyOtherLicense++;
				CacheIrreplacableLicense(articleTitle, conflictLicenses);
			}

			if (errors.Count > 0)
			{
				foreach (string error in errors)
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  " + error);
				}
				worksheet.Text = oldText; //HACK: reset text in case another replacement succeeds
				return false;
			}

			Debug.Assert(creatorDeathYear != 9999);
			Debug.Assert(!string.IsNullOrEmpty(replacedLicense));

			ConsoleUtility.WriteLine(ConsoleColor.Green, "  Replacing '{0}' with '{1}'.", replacedLicense, newLicense);

			bool changed = worksheet.Text != oldText;
			if (changed)
			{
				article.Dirty = true;
				article.Changes.Add(changeText);
			}

			CacheReplacementStatus(articleTitle, ReplacementStatus.Replaced);
			CacheNewLicense(articleTitle, newLicense);

			// remove date mapping
			if (mappedDate != null)
			{
				mappedDate.FromPages.Remove(article.title);
				m_dateMapping.SetDirty();
			}

			return changed;
		}

		private CreatorData GetAuthorData(CommonsFileWorksheet worksheet)
		{
			PageTitle articleTitle = PageTitle.Parse(worksheet.Article.title);
			string author = worksheet.Author;

			if (string.IsNullOrEmpty(author))
			{
				// no author to parse
			}
			else if (ImplicitCreatorsReplacement.IsUnknownAuthor(author)
				|| ImplicitCreatorsReplacement.IsAnonymousAuthor(author))
			{
				// unknown/anonymous author: skip all the expensive searching
			}
			else if (CreatorUtility.TryGetCreatorTemplate(author, out PageTitle literalCreator))
			{
				CreatorData creator = WikidataCache.GetCreatorData(literalCreator);
				if (creator != null)
				{
					return creator;
				}
			}
			else if (Wikidata.TryAuthorToQid(author, out int authorQid))
			{
				CreatorData creator = WikidataCache.GetPersonData(authorQid);
				if (creator != null)
				{
					return creator;
				}
			}
			else if (ImplicitCreatorsReplacement.SlowCategoryWalk)
			{
				// can author be associated to a creator based on file categories?
				//TODO: operate directly on QID instead
				PageTitle categoryCreator = ImplicitCreatorsReplacement.GetCreatorFromCategories(author, WikiUtils.GetCategories(worksheet.Text), 1);
				if (!categoryCreator.IsEmpty)
				{
					CreatorData creator = WikidataCache.GetCreatorData(categoryCreator);
					if (creator != null)
					{
						return creator;
					}
				}
			}

			// D. does author string contain a death date?
			Match match = CreatorUtility.AuthorLifespanRegex.Match(author);
			if (match.Success)
			{
				int deathYear = int.Parse(match.Groups[3].Value);
				if (deathYear <= System.DateTime.Now.Year)
				{
					return new CreatorData() { DeathYear = deathYear };
				}
			}

			// check the artwork wikidata, if it exists
			if (Wikidata.TryUnQidify(worksheet.Wikidata, out int artworkQid))
			{
				CacheArtQID(articleTitle, artworkQid);

				ArtworkData artwork = WikidataCache.GetArtworkData(artworkQid);
				if (artwork.CreatorQid.HasValue)
				{
					return WikidataCache.GetPersonData(artwork.CreatorQid.Value);
				}
			}

			// E. is death year manually mapped?
			MappingCreator mapping = m_creatorMappings.TryMapValue(author, articleTitle);
			if (mapping != null && mapping.MappedDeathyear != 9999)
			{
				return new CreatorData() { DeathYear = mapping.MappedDeathyear };
			}

			return null;
		}

		private int GetLatestPublicationYear(CommonsFileWorksheet worksheet, out MappingDate mappedDate)
		{
			PageTitle articleTitle = PageTitle.Parse(worksheet.Article.title);
			mappedDate = null;

			// check the artwork wikidata, if it exists
			if (Wikidata.TryUnQidify(worksheet.Wikidata, out int artworkQid))
			{
				CacheArtQID(articleTitle, artworkQid);

				ArtworkData artwork = WikidataCache.GetArtworkData(artworkQid);
				if (artwork.LatestYear != 9999)
				{
					return artwork.LatestYear;
				}
			}
			if (string.IsNullOrEmpty(worksheet.Date))
			{
				return 9999;
			}

			DateParseMetadata dateParseMetadata = ParseDate(worksheet.Date);
			if (dateParseMetadata.LatestYear != 9999)
			{
				return dateParseMetadata.LatestYear;
			}

			// unparseable date
			mappedDate = m_dateMapping.TryMapValue(worksheet.Date, PageTitle.Parse(worksheet.Article.title));

			if (!string.IsNullOrEmpty(mappedDate.ReplaceDate))
			{
				// make the date replacement
				{
					ConsoleUtility.WriteLine(ConsoleColor.Green, "  Date '{0}' is mapped to '{1}'.", worksheet.Date, mappedDate.ReplaceDate);

					string textBefore = worksheet.Text.Substring(0, worksheet.DateIndex);
					string textAfter = worksheet.Text.Substring(worksheet.DateIndex + worksheet.Date.Length);
					worksheet.Text = textBefore + mappedDate.ReplaceDate + textAfter;
				}

				dateParseMetadata = ParseDate(mappedDate.ReplaceDate);
				if (dateParseMetadata.LatestYear != 9999)
				{
					return dateParseMetadata.LatestYear;
				}
				else if (mappedDate.LatestYear != 9999)
				{
					return mappedDate.LatestYear;
				}
			}

			return mappedDate.LatestYear;
		}

		private void CacheFile(PageTitle title, string author, string date, string pdArtLicense, string innerLicense)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "INSERT INTO files (pageTitle, authorString, dateString, pdArtLicense, innerLicense) "
				+ "VALUES ($pageTitle, $authorString, $dateString, $pdArtLicense, $innerLicense) "
				+ "ON CONFLICT (pageTitle) DO UPDATE "
				+ "SET authorString=$authorString, dateString=$dateString, pdArtLicense=$pdArtLicense, innerLicense=$innerLicense";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorString", author);
			command.Parameters.AddWithValue("dateString", date);
			command.Parameters.AddWithValue("pdArtLicense", pdArtLicense);
			command.Parameters.AddWithValue("innerLicense", innerLicense);
			Debug.Assert(command.ExecuteNonQuery() == 1);

			CacheTimestamp(title);
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

		private void CacheTimestamp(PageTitle title)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET touchTimeUnix=unixepoch() WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.ExecuteNonQuery();
		}

		private void CacheReplacementStatus(PageTitle title, ReplacementStatus state)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET bLicenseReplaced=$state WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("state", (int)state);
			command.ExecuteNonQuery();
		}

		private void CacheDeathyear(PageTitle title, int deathyear)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET authorDeathYear=$authorDeathYear WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorDeathYear", deathyear);
			command.ExecuteNonQuery();
		}

		private void CacheLatestYear(PageTitle title, int latestYear)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET latestYear=$latestYear WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("latestYear", latestYear);
			command.ExecuteNonQuery();
		}

		private void CacheNewLicense(PageTitle title, string newLicense)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET newLicense=$newLicense WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("newLicense", newLicense);
			command.ExecuteNonQuery();
		}

		private void CacheIrreplacableLicense(PageTitle title, string irreplaceableLicenses)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET irreplaceableLicenses=$irreplaceableLicenses WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("irreplaceableLicenses", irreplaceableLicenses);
			command.ExecuteNonQuery();
		}

		private void CacheArtQID(PageTitle title, int artQid)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET artQid=$artQid WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("artQid", artQid);
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
			date = date.Trim().TrimEnd('.');

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

			// check for "date" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "date");
				if (span.IsValid)
				{
					string dateTemplate = date.Substring(span);
					string year = WikiUtils.GetTemplateParameter(1, dateTemplate);
					if (int.TryParse(year, out int yearInt))
					{
						return new DateParseMetadata(yearInt);
					}
				}
			}

			// check for "taken on" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "taken on");
				if (span.IsValid)
				{
					string takenOnTemplate = date.Substring(span);
					string innerDate = WikiUtils.GetTemplateParameter(1, takenOnTemplate);
					return ParseDate(innerDate);
				}
			}

			// check for "taken in" template
			{
				StringSpan span = WikiUtils.GetTemplateLocation(date, "taken in");
				if (span.IsValid)
				{
					string takenInTemplate = date.Substring(span);
					string innerDate = WikiUtils.GetTemplateParameter(1, takenInTemplate);
					return ParseDate(innerDate);
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

			// "before"
			//TODO: replace with template
			if (date.StartsWith("before ", StringComparison.InvariantCultureIgnoreCase))
			{
				if (int.TryParse(date.Substring("before ".Length), out int beforeInt) && beforeInt >= 100 && beforeInt <= System.DateTime.Now.Year)
				{
					return new DateParseMetadata(9999, 9999, beforeInt - 1);
				}
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
				// padding needed to reach the end of the period
				int padding = (int)Math.Pow(10, imprecision) - 1;

				return new DateParseMetadata(0, periodInt, periodInt + padding);
			}
			else
			{
				return DateParseMetadata.Unknown;
			}
		}
	}
}
