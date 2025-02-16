using MediaWiki;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tasks
{
	public class PdOldAuto : BaseTask
	{
		private const int s_TestLimit = 1;

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

		public override void Execute()
		{
			int successLimit = s_TestLimit;

			foreach (string cat in s_Categories)
			{
				Console.WriteLine("CATEGORY '" + cat + "'...");

				foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(cat, cmtype: CMType.page))
				{
					Article articleContent = GlobalAPIs.Commons.GetPage(article);
					Do(articleContent);
					if (articleContent.Dirty)
					{
						GlobalAPIs.Commons.EditPage(articleContent, articleContent.GetEditSummary());
						successLimit--;
						if (successLimit <= 0) break;
					}
				}
			}
		}

		private static Dictionary<PageTitle, int> s_CreatorToDeathYear = new Dictionary<PageTitle, int>();

		/// <summary>
		/// Gets the year of death from the specified creator template.
		/// </summary>
		/// <param name="errcat">Error category that should be added to the creator.</param>
		public static int GetCreatorDeathYear(PageTitle creator)
		{
			if (!creator.IsNamespace("Creator"))
			{
				return 9999;
			}

			if (s_CreatorToDeathYear.ContainsKey(creator))
			{
				return s_CreatorToDeathYear[creator];
			}
			else
			{
				//TODO: check against Wikidata and publicly report errors

				// check creator
				Article article = GlobalAPIs.Commons.GetPage(creator.ToString());
				if (!Article.IsNullOrEmpty(article))
				{
					article = GlobalAPIs.Commons.FollowRedirects(article);
				}
				if (!Article.IsNullOrEmpty(article))
				{
					CommonsCreatorWorksheet worksheet = new CommonsCreatorWorksheet(article);
					//TODO: check page is a valid creator page

					// get wikidata deathdate
					string wdId = worksheet.Wikidata;
					if (!string.IsNullOrEmpty(wdId))
					{
						Entity wikidata = GlobalAPIs.Wikidata.GetEntity(wdId);
						if (!wikidata.missing && wikidata.HasClaim(Wikidata.Prop_DateOfDeath))
						{
							IEnumerable<MediaWiki.DateTime> deathTimes = wikidata.GetClaimValuesAsDates(Wikidata.Prop_DateOfDeath)
								.Where(date => date != null && date.Precision >= MediaWiki.DateTime.YearPrecision);
							if (deathTimes.Any())
							{
								int deathYear = deathTimes.Max(date => date.GetYear());
								s_CreatorToDeathYear[creator] = deathYear;
								return deathYear;
							}
						}
					}

					// try deathdate from Commons creator
					string deathyear = WikiUtils.GetTemplateParameter("Deathyear", creatorInsides);
					if (string.IsNullOrEmpty(deathyear))
					{
						deathyear = WikiUtils.GetTemplateParameter("Deathdate", creatorInsides);
					}
					if (!string.IsNullOrEmpty(deathyear))
					{
						string[] datesplit = deathyear.Split('-');
						string yearStr = datesplit[0];
						if (yearStr.Length == 4 && int.TryParse(yearStr, out int yearInt))
						{
							s_CreatorToDeathYear[creator] = yearInt;
							return yearInt;
						}
						else
						{
							Console.WriteLine("Creator '" + creator + "' date '" + deathyear + "' malformed.");
							/*string cat = "Category:Creator templates with non-machine-readable birth/death dates";
							if (!Wikimedia.WikiUtils.HasCategory(cat, article.revisions[0].text))
							{
								article.revisions[0].text += "<noinclude>[[" + cat + "]]</noinclude>";
								commonsApi.SetPage(article, "(BOT) couldn't read deathdate", true, true);
							}*/
							s_CreatorToDeathYear[creator] = 9999;
							return 9999;
						}
					}
					else
					{
						Console.WriteLine("Creator '" + creator + "' has no deathdate.");
						s_CreatorToDeathYear[creator] = 9999;
						return 9999;
					}
				}
				else
				{
					Console.WriteLine("Failed to get creator article.");
					s_CreatorToDeathYear[creator] = 9999;
					return 9999;
				}
			}
		}

		/// <summary>
		/// Attempts to update appropriate PD-old templates in the article.
		/// </summary>
		/// <param name="article">Article, already downloaded.</param>
		public static void Do(Article article)
		{
			if (Article.IsNullOrEmpty(article))
			{
				Console.WriteLine("PdOldAuto: FATAL: Article missing.");
				return;
			}

			Console.WriteLine("PdOldAuto: checking '" + article.title + "'.");

			string text = article.revisions[0].text;
			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (string.IsNullOrEmpty(worksheet.Author))
			{
				Console.WriteLine("PdOldAuto: FATAL: No author.");
				return;
			}

			List<Tuple<string, string>> replacements = new List<Tuple<string, string>>();

			int deathyear = 9999;
			if (worksheet.Author.StartsWith("{{Creator:"))
			{
				int creatorEnd = WikiUtils.GetTemplateEnd(worksheet.Author, 0);
				string creatorInner = worksheet.Author.SubstringRange(2, creatorEnd);
				deathyear = GetCreatorDeathYear(PageTitle.Parse(creatorInner));
			}

			if (deathyear == 9999)
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
					if (ReplaceLicenseTemplate(ref text, template, "PD-old-auto", deathyear.ToString()))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto"));
					}
					if (ReplaceLicenseTemplate(ref text, "PD-Art|" + template, "PD-Art|PD-old-auto", deathyear.ToString()))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto"));
					}
				}
				foreach (string template in s_Templates1923)
				{
					if (ReplaceLicenseTemplate(ref text, template, "PD-old-auto-1923", deathyear.ToString()))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1923"));
					}
					if (ReplaceLicenseTemplate(ref text, "PD-Art|" + template, "PD-Art|PD-old-auto-1923", deathyear.ToString()))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1923"));
					}
				}
				foreach (string template in s_Templates1996)
				{
					if (ReplaceLicenseTemplate(ref text, template, "PD-old-auto-1996", deathyear.ToString()))
					{
						replacements.Add(new Tuple<string, string>(template, "PD-old-auto-1996"));
					}
					if (ReplaceLicenseTemplate(ref text, "PD-Art|" + template, "PD-Art|PD-old-auto-1996", deathyear.ToString()))
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
