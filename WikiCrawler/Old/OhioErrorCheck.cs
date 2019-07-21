using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WikiCrawler
{
	class OhioErrorCheck
	{
		public static void Harvest()
		{
			MediaWiki.Api api = new MediaWiki.Api(new Uri("http://en.wikipedia.org/"));

			MediaWiki.Article baseArticle = api.GetPage("User:Nyttend/County templates");
			string section = baseArticle.revisions[0].text
				.Split(new string[] {"==Ohio=="}, StringSplitOptions.None)[1]
				.Split(new string[] {"=="}, 2, StringSplitOptions.None)[0];

			string[] splitters = new string[] { "{{tl|", "}}, {{tl|", "}}" };
			string[] counties = section.Trim().Split(splitters, StringSplitOptions.RemoveEmptyEntries);

			int testLimit = int.MaxValue;

			using (StreamWriter log = new StreamWriter(new FileStream("ohio_log.txt", FileMode.Create)))
			{
				foreach (string county in counties)
				{
					Console.WriteLine(county);
					MediaWiki.Article countyArticle = api.GetPage("Template:" + county);

					string countyFirst = county.Substring(0, county.Length - " County, Ohio".Length);

					string[] lines = countyArticle.revisions[0].text.Split('\n');
					bool enabled = false;
					foreach (string line in lines)
					{
						// make sure we only look at Cities, Villages, Townships
						if (line.Contains("[[City (Ohio)|Cit"))
						{
							Console.WriteLine("Found cities");
							enabled = true;
						}
						else if (line.Contains("[[Village (Ohio)|Village"))
						{
							Console.WriteLine("Found villages");
							enabled = true;
						}
						else if (line.Contains("[[Civil township|Township"))
						{
							Console.WriteLine("Found townships");
							enabled = true;
						}
						else if (string.IsNullOrEmpty(line.Trim()))
						{
							enabled = false;
						}

						if (enabled && line.StartsWith("*"))
						{
							string settlement = line
								.Split(new string[] { "[[" }, 2, StringSplitOptions.None)[1]
								.Split('|')[0];
							string settlementFirst = settlement.Split(',')[0].Trim();

							testLimit--;
							if (testLimit <= 0) return;

							Console.WriteLine("-- " + settlement);

							MediaWiki.Article article = api.GetPage(settlement);

							if (article == null || article.missing)
							{
								log.WriteLine(settlement + ":- article not found");
							}
							else if (!article.revisions[0].text.Contains("{{Infobox settlement"))
							{
								log.WriteLine(settlement + ":- no 'Infobox settlement'");
							}
							else
							{
								string[] split1 = article.revisions[0].text.Split(
									new string[] { " County Ohio Highlighting " }, 2, StringSplitOptions.None);

								if (split1.Length != 2)
								{
									continue;
								}

								string[] split2 = split1[0].Split(
									new string[] { "= Map of " }, 2, StringSplitOptions.None);

								if (split2.Length != 2)
								{
									log.WriteLine(settlement + ":- !!! Map of split failed");
									continue;
								}

								string[] split3 = split1[1].Split(
									new string[] { ".png" }, 2, StringSplitOptions.None);

								if (split3.Length != 2)
								{
									log.WriteLine(settlement + ":- !!! png split failed");
									continue;
								}

								if (split2[1] != countyFirst)
								{
									log.WriteLine(settlement + ":- image_map wrong county '" + split2[1] + "', expected '" + countyFirst + "'");
								}
								if (split3[0] != settlementFirst
									&& split3[0] != settlementFirst + " City"
									&& split3[0] != settlementFirst + " Village"
									&& split3[0] != settlementFirst + " Township")
								{
									log.WriteLine(settlement + ":- image_map wrong settlement '" + split3[0] + "'");
								}

								string[] split4 = split3[1].Split(
									new string[] { "= Location of " }, 2, StringSplitOptions.None);

								if (split4.Length != 2)
								{
									log.WriteLine(settlement + ":- !!! Location of split failed");
									continue;
								}

								string[] split5 = split4[1].Split(
									new string[] { " in " }, 2, StringSplitOptions.None);

								if (split5.Length != 2)
								{
									log.WriteLine(settlement + ":- !!! in split failed");
									continue;
								}

								string[] split6 = split5[1].Split(
									new string[] { " County" }, 2, StringSplitOptions.None);

								if (split6.Length != 2)
								{
									log.WriteLine(settlement + ":- !!! County split failed");
									continue;
								}

								if (split6[0] != countyFirst)
								{
									log.WriteLine(settlement + ":- map_caption wrong county '" + split6[0] + "', expected '" + countyFirst + "'");
								}
								if (split5[0] != settlementFirst
									&& split5[0] != settlementFirst + " City"
									&& split5[0] != settlementFirst + " Village"
									&& split5[0] != settlementFirst + " Township")
								{
									log.WriteLine(settlement + ":- map_caption wrong settlement '" + split5[0] + "'");
								}
							}
						}
					}
				}
			}
		}
	}
}
