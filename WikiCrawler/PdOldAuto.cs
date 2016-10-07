using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WikiCrawler
{
	static class PdOldAuto
	{
		private const int s_TestLimit = 1;

		private static string[] s_DoMe =
@"File:Nicolaes Eliasz. Pickenoy - Self-Portrait at the Age of Thirty-Six - WGA17440.jpg
File:Jochem Hendricksz Swartenhont (1566-1627), by Nicolaes Eliasz Pickenoy.jpg
File:Portrait of an unknown man, by Nicolaes Eliasz Pickenoy.jpg".Split('\n');

		private static List<string> s_Categories = new List<string>()
		{
			"Category:PD-old-50-1923",
			"Category:PD-old-60-1923",
			"Category:PD-old-70-1923",
			"Category:PD-old-75-1923",
			"Category:PD-old-80-1923",
			"Category:PD-old-90-1923",
			"Category:PD-old-100-1923",
			"Category:PD 1923", "PD-1923",
			
			"Category:PD-old-50-1996",
			"Category:PD-old-60-1996",
			"Category:PD-old-70-1996",
			"Category:PD-old-75-1996",
			"Category:PD-old-80-1996",
			"Category:PD-old-90-1996",
			"Category:PD-old-100-1996",
			"Category:PD 1996",
		};

		private static List<string> s_TemplatesOld = new List<string>()
		{
			"PD-old-50",
			"PD-old-60",
			"PD-old-70",
			"PD-old-75",
			"PD-old-80",
			"PD-old-90",
			"PD-old-100",
			"PD-old",
		};

		private static List<string> s_Templates1923 = new List<string>()
		{
			"PD-old-50-1923",
			"PD-old-60-1923",
			"PD-old-70-1923",
			"PD-old-75-1923",
			"PD-old-80-1923",
			"PD-old-90-1923",
			"PD-old-100-1923",
			"PD-old-X-1923",
			"PD-1923",
		};

		//TODO: BE SURE TO USE COUNTRY PARAM
		private static List<string> s_Templates1996 = new List<string>()
		{
			"PD-old-50-1996",
			"PD-old-60-1996",
			"PD-old-70-1996",
			"PD-old-75-1996",
			"PD-old-80-1996",
			"PD-old-90-1996",
			"PD-old-100-1996",
			"PD-old-X-1996",
			"PD-1996",
		};

		//TODO: also fix [[Category:PD Old auto: no death date]]

		public static void Do()
		{
			Console.WriteLine("Logging in...");
			Wikimedia.WikiApi commonsApi = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org"));
			commonsApi.LogIn();
			Wikimedia.WikiApi wikidataApi = new Wikimedia.WikiApi(new Uri("https://www.wikidata.org"));
			commonsApi.LogIn();

			foreach (string art in s_DoMe)
			{
				Wikimedia.Article articleContent = commonsApi.GetPage(art.Trim());
				Do(commonsApi, wikidataApi, articleContent);
				if (articleContent.Dirty)
				{
					commonsApi.SetPage(articleContent, articleContent.GetEditSummary(), false, true, true);
				}
			}
			return;

			int successLimit = s_TestLimit;

			foreach (string cat in s_Categories)
			{
				Console.WriteLine("CATEGORY '" + cat + "'...");

				foreach (Wikimedia.Article article in commonsApi.GetCategoryPages(cat))
				{
					Wikimedia.Article articleContent = commonsApi.GetPage(article);
					Do(commonsApi, wikidataApi, articleContent);
					if (articleContent.Dirty)
					{
						commonsApi.SetPage(articleContent, articleContent.GetEditSummary(), false, true, true);
						successLimit--;
						if (successLimit <= 0) break;
					}
				}
			}
		}

		private static char[] s_CreatorTrim = new char[] { ' ', '{', '}' };

		private static Dictionary<string, string> s_CreatorToDeathYear = new Dictionary<string, string>();

		/// <summary>
		/// Gets the year of death from the specified creator template.
		/// </summary>
		/// <param name="errcat">Error category that should be added to the creator.</param>
		private static string GetCreatorDeathYear(Wikimedia.WikiApi commonsApi, Wikimedia.WikiApi wikidataApi,
			string creator)
		{
			creator = creator.Trim(s_CreatorTrim);

			if (s_CreatorToDeathYear.ContainsKey(creator))
			{
				return s_CreatorToDeathYear[creator];
			}
			else
			{
				//TODO: check against Wikidata and publicly report errors
				
				// check creator
				Wikimedia.Article article = commonsApi.GetPage(creator);
				if (!Wikimedia.Article.IsNullOrEmpty(article))
				{
					// get wikidata deathdate
					string wdDeathYear = "";
					string wdId = Wikimedia.WikiUtils.GetTemplateParameter("Wikidata", article.revisions[0].text);
					if (!string.IsNullOrEmpty(wdId))
					{
						Wikimedia.Entity wikidata = wikidataApi.GetEntity(wdId);
						if (!wikidata.missing)
						{
							wdDeathYear = wikidata.GetClaimValueAsDate(Wikimedia.Wikidata.Prop_DateOfDeath).GetYear().ToString();
						}
					}

					string deathyear = Wikimedia.WikiUtils.GetTemplateParameter("Deathyear", article.revisions[0].text);
					if (string.IsNullOrEmpty(deathyear))
					{
						deathyear = Wikimedia.WikiUtils.GetTemplateParameter("Deathdate", article.revisions[0].text);
					}
					if (!string.IsNullOrEmpty(deathyear))
					{
						string[] datesplit = deathyear.Split('-');
						string year = datesplit[0];
						if (year.Length == 4
							&& char.IsDigit(year[0])
							&& char.IsDigit(year[1])
							&& char.IsDigit(year[2])
							&& char.IsDigit(year[3]))
						{
							s_CreatorToDeathYear[creator] = year;
							return year;
						}
						else
						{
							/*Console.WriteLine("Creator '" + creator + "' date '" + deathyear + "' malformed.");
							string cat = "Category:Creator templates with non-machine-readable birth/death dates";
							if (!Wikimedia.WikiUtils.HasCategory(cat, article.revisions[0].text))
							{
								article.revisions[0].text += "<noinclude>[[" + cat + "]]</noinclude>";
								commonsApi.SetPage(article, "(BOT) couldn't read deathdate", true, true);
							}*/
							return wdDeathYear;
						}
					}
					else
					{
						Console.WriteLine("Creator '" + creator + "' has no deathdate.");
						return "";
					}
				}
				else
				{
					Console.WriteLine("Failed to get creator article.");
					return "";
				}
			}
		}

		/// <summary>
		/// Attempts to update appropriate PD-old templates in the article.
		/// </summary>
		/// <param name="article">Article, already downloaded.</param>
		public static void Do(Wikimedia.WikiApi commonsApi, Wikimedia.WikiApi wikidataApi,
			Wikimedia.Article article)
		{
			if (Wikimedia.Article.IsNullOrEmpty(article))
			{
				Console.WriteLine("PdOldAuto: FATAL: Article missing.");
				return;
			}

			Console.WriteLine("PdOldAuto: checking '" + article.title + "'.");

			string text = article.revisions[0].text;

			// search for creator
			string author = Wikimedia.WikiUtils.GetTemplateParameter("artist", text);
			if (string.IsNullOrEmpty(author))
			{
				author = Wikimedia.WikiUtils.GetTemplateParameter("author", text);
			}
			author = author.Trim();

			if (string.IsNullOrEmpty(author))
			{
				Console.WriteLine("PdOldAuto: FATAL: No author.");
				return;
			}

			List<Tuple<string, string>> replacements = new List<Tuple<string, string>>();

			string deathyear = "";
			if (author.StartsWith("{{Creator:"))
			{
				deathyear = GetCreatorDeathYear(commonsApi, wikidataApi, author);
			}

			if (string.IsNullOrEmpty(deathyear))
			{
				Console.WriteLine("PdOldAuto: FATAL: No deathyear found.");
			}
			else
			{
				Console.WriteLine("PdOldAuto: deathyear is '" + deathyear + "'.");

				// search for auto templates missing deathdate
				//TODO:

				// search for non-auto templates to replace
				foreach (string template in s_TemplatesOld)
				{
					if (ReplaceLicenseTemplate(ref text, template, "PD-old-auto", deathyear))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto"));
					}
					if (ReplaceLicenseTemplate(ref text, "PD-Art|" + template, "PD-Art|PD-old-auto", deathyear))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto"));
					}
				}
				foreach (string template in s_Templates1923)
				{
					if (ReplaceLicenseTemplate(ref text, template, "PD-old-auto-1923", deathyear))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1923"));
					}
					if (ReplaceLicenseTemplate(ref text, "PD-Art|" + template, "PD-Art|PD-old-auto-1923", deathyear))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1923"));
					}
				}
				foreach (string template in s_Templates1996)
				{
					if (ReplaceLicenseTemplate(ref text, template, "PD-old-auto-1996", deathyear))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1996"));
					}
					if (ReplaceLicenseTemplate(ref text, "PD-Art|" + template, "PD-Art|PD-old-auto-1996", deathyear))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1996"));
					}
				}
			}

			if (article.revisions[0].text != text)
			{
				StringBuilder summary = new StringBuilder();
				summary.Append("Refine license tag based on Creator: ");
				foreach (Tuple<string, string> rep in replacements)
				{
					summary.Append("{{[[Template:" + rep.Item1 + "|" + rep.Item1 + "]]}}→{{[[Template:" + rep.Item2 + "|" + rep.Item2 + "]]}}");
					summary.Append(", ");
				}
				summary.Remove(summary.Length - 2, 2);

				article.revisions[0].text = text;
				article.Changes.Add(summary.ToString());
				article.Dirty = true;
			}
		}

		public static bool ReplaceLicenseTemplate(ref string text, string oldTemplate, string newTemplate, string deathyear)
		{
			string find = "{{" + oldTemplate;
			int templateLoc = text.IndexOf(find);
			if (templateLoc >= 0)
			{
				//make sure that there's a break here
				int templateNextIndex = templateLoc + find.Length;
				if (text[templateNextIndex] == '|' || text[templateNextIndex] == '}')
				{
					string newLicense = "{{" + newTemplate + "|deathyear=" + deathyear;
					text = text.Substring(0, templateLoc)
						+ newLicense
						+ text.Substring(templateNextIndex, text.Length - templateNextIndex);
					return true;
				}
			}
			return false;
		}
	}
}
