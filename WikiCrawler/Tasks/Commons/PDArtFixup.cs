using MediaWiki;
using System;
using System.Collections.Generic;
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
	public static class PDArtFixup
	{
		/// <summary>
		/// If true, will use previously cached files as a test. Will not make edits.
		/// </summary>
		private const bool UseCachedFiles = false;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "pdartfixup"); }
		}

		private static string FileCacheDirectory
		{
			get { return Path.Combine(ProjectDataDirectory, "cache"); }
		}

		private static List<Regex> s_dateRegexes = new List<Regex>();
		private static Regex s_sRegex = new Regex("^([0-9]+)'?[Ss]$");

		static PDArtFixup()
		{
			Directory.CreateDirectory(FileCacheDirectory);

			foreach (string regex in File.ReadAllLines(Path.Combine(ProjectDataDirectory, "date-regexes.txt")))
			{
				if (!string.IsNullOrWhiteSpace(regex))
				{
					s_dateRegexes.Add(new Regex(regex));
				}
			}
		}

		//[BatchTask]
		public static void Cache()
		{
			foreach (Article file in GetFilesToAffectUncached(""))
			{
				Console.WriteLine(file.title);

				string filename = Path.Combine(FileCacheDirectory, string.Concat(file.title.Split(Path.GetInvalidFileNameChars())));
				if (filename.Length > 250)
				{
					filename = filename.Substring(0, 250);
				}
				filename = filename + ".txt";
				if (File.Exists(filename))
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("  File exists");
					Console.ResetColor();
				}
				File.WriteAllText(filename, file.title + "\n" + file.revisions[0].text, Encoding.UTF8);
			}
		}

		private static IEnumerable<Article> GetFilesToAffect(string startSortkey)
		{
			if (UseCachedFiles)
			{
				return GetFilesToAffectCached();
			}
			else
			{
				return GetFilesToAffectUncached(startSortkey);
			}
		}

		private static IEnumerable<Article> GetFilesToAffectCached()
		{
			char[] filesplitter = new char[] { '\n' };
			foreach (string path in Directory.GetFiles(FileCacheDirectory))
			{
				string text = File.ReadAllText(path, Encoding.UTF8);
				string[] split = text.Split(filesplitter, 2);
				Article article = new Article(split[0]);
				article.revisions = new Revision[] { new Revision() { text = split[1] } };
				yield return article;
			}
		}

		private static IEnumerable<Article> GetFilesToAffectUncached(string startSortkey)
		{
			//TODO: runs out of memory
			IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries("Category:PD-Art (PD-old default)", CMType.file, cmstartsortkeyprefix: startSortkey);
			while (true)
			{
				IEnumerable<Article> theseFiles = allFiles.Take(50);

				if (!theseFiles.Any())
				{
					break;
				}

				Article[] filesGot = GlobalAPIs.Commons.GetPages(theseFiles.ToList(), prop: "info|revisions");

				foreach (Article file in filesGot)
				{
					yield return file;
				}

				allFiles = allFiles.Skip(50);
			}
		}

		private static int qtyProcessed = 0;
		private static int qtyInfoFindFail = 0;
		private static int qtyDateParseFail = 0;
		private static int qtyNotPDUS = 0;
		private static int qtyNoCreator = 0;
		private static int qtyNoDeathYear = 0;
		private static int qtyInsufficientPMA = 0;
		private static int qtyOtherLicense = 0;
		private static int qtySuccess = 0;

		private static Dictionary<string, int> unparsedDates = new Dictionary<string, int>();

		[BatchTask]
		public static void Run()
		{
			//TODO: actually use this
			string startSortkey = "";
			string progressFile = Path.Combine(ProjectDataDirectory, "checkpoint.txt");
			if (File.Exists(progressFile))
			{
				startSortkey = File.ReadAllText(progressFile);
			}

			int maxReads = 100;
			int maxEdits = 2;

			int saveOutInterval = 1;
			int saveOutCounter = 0;

			foreach (Article file in GetFilesToAffect(startSortkey))
			{
				if (maxEdits <= 0 || maxReads <= 0)
				{
					break;
				}

				maxReads--;

				// save out stats
				if (saveOutCounter >= saveOutInterval)
				{
					//NOTE: may miss the last batch of files

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
					string datesText = unparsedDates.OrderByDescending(kv => kv.Value).Aggregate("", (text, kv) => text + "\n" + kv.Value.ToString("00000") + " " + kv.Key);
					string creatorsText = "";// creatorsMissingDeathyear.OrderByDescending(kv => kv.Value).Aggregate("", (text, kv) => text + "\n" + kv.Value.ToString("00000") + " " + kv.Key);
					File.WriteAllText(Path.Combine(ProjectDataDirectory, "pdartfixup.txt"), statsText + "\n\n" + datesText + "\n\n" + creatorsText, Encoding.UTF8);

					saveOutCounter -= saveOutInterval;
				}
				saveOutCounter++;

				// record sortkey progress
				File.WriteAllText(progressFile, WikiUtils.GetSortkey(file));

				qtyProcessed++;

				// we can also attempt to insert the creator if missing
				// do this first in case it manages to map a creator, we can use that for deathdate
				bool implicitCreatorReplaced = FixImplicitCreators.Do(file);

				// try to replace PD-Art
				bool pdArtReplaced = Do(file);

				if (!implicitCreatorReplaced)
				{
					//TEMP: TESTING
					continue;
				}

				if (file.Dirty && !UseCachedFiles)
				{
					CommonsCreatorFromWikidata.FixInformationTemplates(file);
					GlobalAPIs.Commons.EditPage(file, file.GetEditSummary());
					qtySuccess++;
				}

				maxEdits--;
			}
		}

		/// <summary>
		/// Determines the license that can replace PD-Art and replaces it.
		/// </summary>
		/// <returns>True if a replacement was made.</returns>
		public static bool Do(Article article)
		{
			Console.WriteLine("PDArtFixup: checking '{0}'...", article.title);

			string text = article.revisions[0].text;
			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (string.IsNullOrEmpty(worksheet.Author))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Failed to find author.");
				Console.ResetColor();
				qtyInfoFindFail++;
				return false;
			}
			if (string.IsNullOrEmpty(worksheet.Date))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Failed to find date.");
				Console.ResetColor();
				qtyInfoFindFail++;
				return false;
			}

			// try to parse date
			DateParseMetadata dateParseMetadata = ParseDate(worksheet.Date);
			if (dateParseMetadata.LatestYear == 9999)
			{
				if (!unparsedDates.ContainsKey(worksheet.Date))
				{
					unparsedDates[worksheet.Date] = 1;
				}
				else
				{
					unparsedDates[worksheet.Date]++;
				}

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Failed to parse date '{0}'.", worksheet.Date);
				Console.ResetColor();
				qtyDateParseFail++;
				return false;
			}
			else if (dateParseMetadata.LatestYear >= System.DateTime.Now.Year - 95)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Date {0} is after the US expired threshold.", dateParseMetadata.LatestYear);
				Console.ResetColor();
				qtyNotPDUS++;
				return false;
			}

			Creator creator = CreatorUtility.GetCreator(worksheet.Author);
			creator.Usage++;
			if (creator.DeathYear == 9999)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Can't determine death year for creator '{0}'.", worksheet.Author);
				Console.ResetColor();
				qtyNoDeathYear++;
				return false;
			}

			int pmaYear = System.DateTime.Now.Year - 120;
			if (creator.DeathYear >= pmaYear)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Death year {0} is inside max PMA.", creator.DeathYear);
				Console.ResetColor();
				qtyInsufficientPMA++;
				return false;
			}

			if (text.Replace("{{PD-Art", "").Contains("{{PD"))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Contains other licenses.", creator.DeathYear);
				Console.ResetColor();
				qtyOtherLicense++;
				return false;
			}

			string newLicense = string.Format("{{{{PD-Art|PD-old-auto-expired|deathyear={0}}}}}", creator.DeathYear);

			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("  PDArtFixup replacing PD-Art with '{0}'.", newLicense);
			Console.ResetColor();

			//TODO: what if PD-Art doesn't look like this?
			article.revisions[0].text = text.Replace("{{PD-Art}}", newLicense);
			article.Dirty = true;
			article.Changes.Add("replacing PD-art with a more accurate license based on file data");
			return true;
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

			// check against various regexes
			foreach (Regex regex in s_dateRegexes)
			{
				Match match = regex.Match(date);
				if (match.Success)
				{
					DateParseMetadata? metadata = null;
					for (int g = 1; g < match.Groups.Count; ++g)
					{
						Group group = match.Groups[g];
						if (metadata == null)
						{
							metadata = ParseDate(group.Value);
						}
						else
						{
							metadata = DateParseMetadata.Combine(metadata.Value, ParseDate(group.Value));
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
							int magnitude = 10 ^ length2;
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
