using MediaWiki;
using System;
using System.IO;
using System.Text.RegularExpressions;
using WikiCrawler;

namespace Tasks
{
	public struct RegexReplacement
	{
		public Regex Regex;
		public string Replacement;

		public RegexReplacement(string inRegex, string inReplacement)
		{
			Regex = new Regex(inRegex);
			Replacement = inReplacement;
		}

		public RegexReplacement(Regex inRegex, string inReplacement)
		{
			Regex = inRegex;
			Replacement = inReplacement;
		}
	}

	/// <summary>
	/// Replaces non-localizable dates with localizable ones.
	/// </summary>
	public class LocalizeDateReplacement : BaseReplacement
	{
		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "localizedate"); }
		}

		public static string ReplacementsLogFile
		{
			get { return Path.Combine(ProjectDataDirectory, "replacements.txt"); }
		}

		private static RegexReplacement[] s_replacements = new RegexReplacement[]
		{
			new RegexReplacement(new Regex(
				@"^([0-9\\-–]+s?)\\. Public domain$", RegexOptions.IgnoreCase),
				"$1"),

			new RegexReplacement(new Regex(
				@"^late ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|late|$1}}"),
			//new RegexReplacement(@"^late ([0-9]+'?s)$", "{{complex date|adj1=late|precision1=TODO|date1=$1}}")
			new RegexReplacement(new Regex(
				@"^early ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|early|$1}}"),

			new RegexReplacement(new Regex(
				@"^ca[\\.,]? ?([0-9\\-]+)$", RegexOptions.IgnoreCase),
				"{{other date|circa|$1}}"),
			new RegexReplacement(new Regex(
				@"^circa[\. ]*([0-9\\-]+)\.?$"	, RegexOptions.IgnoreCase),
				"{{other date|circa|$1}}"),

			new RegexReplacement(new Regex(
				@"^entre ([0-9]+) [ey] ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|between|$1|$2}}"),
			new RegexReplacement(new Regex(
				@"^between ([0-9]+) and ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|between|$1|$2}}"),
			new RegexReplacement(new Regex(
				@"^anterior a ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|before|$1}}"),
			new RegexReplacement(new Regex(
				@"^before ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|before|$1}}"),
			new RegexReplacement(new Regex(
				@"^po roce ([0-9]+)$", RegexOptions.IgnoreCase),
				"{{other date|after|$1}}"),

			new RegexReplacement(new Regex(
				@"^desconocid[ao]$", RegexOptions.IgnoreCase),
				"{{unknown|date}}"),
			new RegexReplacement(new Regex(
				@"^exact publication date unknown$", RegexOptions.IgnoreCase),
				"{{unknown|date}}"),
			new RegexReplacement(new Regex(
				@"^non connue$", RegexOptions.IgnoreCase),
				"{{unknown|date}}"),

			new RegexReplacement(new Regex(
				@"^([0-9]+)[sthrd] [Cc]entury$", RegexOptions.IgnoreCase),
				"{{other date|century|$1}}"),
			new RegexReplacement(new Regex(
				@"^circa late ([0-9][0-9]?)[srdth]* century$", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|adj1=late|precision1=century}}"),
			new RegexReplacement(new Regex(
				@"^later ([0-9][0-9]?)[srdth]* [Cc]entury CE$", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|adj1=late|precision1=century}}"),
			new RegexReplacement(new Regex(
				@"^mid[\- ]([0-9][0-9]?)[srdth]* century$", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|adj1=mid|precision1=century}}"),

			new RegexReplacement(new Regex(
				@"^([0-9][0-9]?)[srdth]* or ([0-9][0-9]?)[srdth]* centuries ?[ADCE]*$", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|precision1=century|conj=or|date2=$2|precision2=century}}"),
			new RegexReplacement(new Regex(
				@"^([0-9][0-9]?)[srdth]*/([0-9][0-9]?)[srdth]* century ?[ADCE]*$", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|precision1=century|conj=or|date2=$2|precision2=century}}"),
			new RegexReplacement(new Regex(
				@"^([0-9][0-9]?)\.\-([0-9][0-9]?)\.\. století$", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|precision1=century|conj=-|date2=$2|precision2=century}}"),
			new RegexReplacement(new Regex(
				@"^([0-9][0-9]?00)'?s?\-([0-9][0-9]?00)'?s?", RegexOptions.IgnoreCase),
				"{{complex date|date1=$1|precision1=century|conj=-|date2=$2|precision2=century}}"),
		};

		public override bool DoReplacement(Article article)
		{
			//TODO: test
			return false;

			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			bool bReplaced = false;
			foreach (RegexReplacement replacement in s_replacements)
			{
				Match match = replacement.Regex.Match(worksheet.Date);
				if (match.Success)
				{
					// interpolate groups into result
					string replacementString = replacement.Replacement;
					for (int i = 1; i < match.Groups.Count; i++)
					{
						replacementString = replacementString.Replace("$" + i, match.Groups[i].Value);
					}

					ConsoleUtility.WriteLine(ConsoleColor.Green, "  LocalizeDateReplacement '{0}'->'{1}'", worksheet.Date, replacementString);

					// replace date
					string oldText = worksheet.Text;
					worksheet.Text = oldText.Substring(0, worksheet.DateIndex) + replacementString + oldText.Substring(worksheet.DateIndex + worksheet.Date.Length);

					bReplaced = true;
				}
			}

			if (bReplaced)
			{
				article.Changes.Add("localizable date");
				// minor: does not dirty: article.Dirty = true;
				return false;
			}
			else
			{
				return false;
			}
		}
	}
}
