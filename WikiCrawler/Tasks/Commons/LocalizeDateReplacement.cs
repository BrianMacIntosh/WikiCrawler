using MediaWiki;
using System;
using System.Text.RegularExpressions;

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
		private static RegexReplacement[] s_replacements = new RegexReplacement[]
		{
			new RegexReplacement(new Regex(@"^[Ll]ate ([0-9]+)$"), "{{other date|late|$1}}"),
			//new RegexReplacement(@"^late ([0-9]+'?s)$", "{{complex date|adj1=late|precision1=TODO|date1=$1}}")
			new RegexReplacement(new Regex(@"^[Ee]arly ([0-9]+)$"), "{{other date|early|$1}}"),
			new RegexReplacement(new Regex(@"^([0-9\\-–]+s?)\\. Public domain$"), "$1"),
			new RegexReplacement(new Regex(@"^ca[\\.,]? ?([0-9\\-]+)$"), "{{other date|circa|$1}}"),
			new RegexReplacement(new Regex(@"^[Cc]irca[\. ]*([0-9\\-]+)\.?$"), "{{other date|circa|$1}}"),
			new RegexReplacement(new Regex(@"^[Dd]esconocid[ao]$"), "{{unknown|date}}"),
			new RegexReplacement(new Regex(@"^([0-9]+)[sthrd] [Cc]entury$"), "{{other date|century|$1}}"),
			new RegexReplacement(new Regex(@"^entre ([0-9]+) [ey] ([0-9]+)$"), "{{other date|between|$1|$2}}"),
			new RegexReplacement(new Regex(@"^between ([0-9]+) and ([0-9]+)$"), "{{other date|between|$1|$2}}"),
			new RegexReplacement(new Regex(@"^anterior a ([0-9]+)$"), "{{other date|before|$1}}"),
		};

		public override bool DoReplacement(Article article)
		{
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

					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("  LocalizeDateReplacement '{0}'->'{1}'", worksheet.Date, replacementString);
					Console.ResetColor();

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
