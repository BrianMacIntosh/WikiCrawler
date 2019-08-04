using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Tasks
{
	public static class CommonsCreatorFromWikidata
	{
		private static Api Wikidata = new Api(new Uri("https://www.wikidata.org/"));

		//TODO: support BC dates

		private static bool s_DoBotCat = false;
		private static bool s_DoNormalCat = true;
		private static int s_TestLimit = int.MaxValue;
		private static bool s_MakeNewCreators = false;
		private static bool s_FixImplicitCreators = true;

		/// <summary>
		/// Creates creator templates as indicated by 'Category:Creator templates to be created by a bot'.
		/// </summary>
		[BatchTask]
		public static void MakeCreatorsFromCat()
		{
			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org"));

			Console.WriteLine("Logging in...");
			commonsApi.AutoLogIn();
			Wikidata.AutoLogIn();

			int successLimit = s_TestLimit;

			string normalCatLastPage = "";

			if (File.Exists("autocreators/stored.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("autocreators/stored.txt", FileMode.Open), Encoding.Default))
				{
					normalCatLastPage = reader.ReadLine();
				}
			}

			try
			{
				if (s_DoBotCat)
				{
					foreach (Article article in commonsApi.GetCategoryEntries("Category:Creator templates to be created by a bot", cmtype: CMType.subcat))
					{
						Console.WriteLine("Checking verified '" + article.title + "'...");

						if (!article.title.StartsWith("Category:"))
						{
							Console.WriteLine("Error: not a category.");
							continue;
						}

						Article articleContent = commonsApi.GetPage(article);
						string text = articleContent.revisions[0].text;

						// find template parameter
						string wikidataId = WikiUtils.GetTemplateParameter("wikidata", text);
						Console.WriteLine("Wikidata Id is " + wikidataId);

						if (ProcessCreatorCategory(commonsApi, articleContent, wikidataId))
						{
							--successLimit;
							if (successLimit <= 0)
							{
								return;
							}
						}
					}
				}

				if (s_DoNormalCat)
				{
					foreach (Article article in commonsApi.GetCategoryEntries("Category:People by name", cmtype: CMType.subcat, cmstartsortkeyprefix: normalCatLastPage))
					{
						Console.WriteLine("----- Checking '" + article.title + "'...");

						if (!article.title.StartsWith("Category:"))
						{
							Console.WriteLine("Error: not a category.");
							continue;
						}

						Article articleContent = commonsApi.GetPage(article);
						string text = articleContent.revisions[0].text;

						string name = article.title.Substring("Category:".Length);
						string wikidataId = "";

						// check creator for a wikidata idea
						if (string.IsNullOrEmpty(wikidataId))
						{
							string creatorPre = "{{Creator:";
							int creatorIndex = text.IndexOf(creatorPre);
							if (creatorIndex >= 0)
							{
								int creatorEnd = text.IndexOf("}}", creatorIndex);
								string creator = text.Substring(creatorIndex + 2, creatorEnd - (creatorIndex + 2));

								Article creatorArt = commonsApi.GetPage(creator);
								if (!MediaWiki.Article.IsNullOrEmpty(creatorArt))
								{
									Console.WriteLine("Checking Creator for wikidata id");
									wikidataId = MediaWiki.WikiUtils.GetTemplateParameter("wikidata", creatorArt.revisions[0].text);
								}
							}
						}

						// check for On Wikidata
						if (string.IsNullOrEmpty(wikidataId))
						{
							string onWikidataTemplate;
							MediaWiki.WikiUtils.RemoveTemplate("On Wikidata", text, out onWikidataTemplate);
							if (!string.IsNullOrEmpty(onWikidataTemplate))
							{
								string[] split = onWikidataTemplate.Split('|');
								if (split.Length == 2)
								{
									Console.WriteLine("Matched ON WIKIDATA");
									wikidataId = split[1];
								}
							}
						}

						if (string.IsNullOrEmpty(wikidataId))
						{
							// look for DOB and DOD categories
							string yearOfBirth = "";
							string yearOfDeath = "";
							foreach (string cat in MediaWiki.WikiUtils.GetCategories(text))
							{
								//HACK: only works with 4-digit years
								if (cat.EndsWith(" births"))
								{
									yearOfBirth = cat.Substring("Category:".Length, 4);
								}
								else if (cat.EndsWith(" deaths"))
								{
									yearOfDeath = cat.Substring("Category:".Length, 4);
								}
							}

							if (string.IsNullOrEmpty(yearOfBirth) || string.IsNullOrEmpty(yearOfDeath))
							{
								Console.WriteLine("Insufficient information.");
							}
							else
							{
								wikidataId = GetWikidataId(article.GetTitle(), yearOfBirth, yearOfDeath);
								if (!string.IsNullOrEmpty(wikidataId))
								{
									Console.WriteLine("Got wikidata from Creator");
								}
							}
						}

						if (!string.IsNullOrEmpty(wikidataId))
						{
							Console.WriteLine("++ Matched " + wikidataId + ".");

							if (ProcessCreatorCategory(commonsApi, articleContent, wikidataId))
							{
								--successLimit;
								if (successLimit <= 0)
								{
									Console.WriteLine("Hit limit.");
									return;
								}
							}
						}
						else
						{
							Console.WriteLine("Couldn't determine Wikidata ID.");
						}

						normalCatLastPage = MediaWiki.WikiUtils.GetSortkey(article);
					}
				}
			}
			finally
			{
				using (StreamWriter writer = new StreamWriter(new FileStream("implicitcreators/stored.txt", FileMode.Create), Encoding.Default))
				{
					writer.WriteLine(normalCatLastPage);
				}
			}
		}

		/// <summary>
		/// Searches for a wikidata entity matching the specified info.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="yob"></param>
		/// <param name="yod"></param>
		private static string GetWikidataId(string name, string yearOfBirth, string yearOfDeath)
		{
			// search wikidata
			string[] search = Wikidata.SearchEntities(name);
			foreach (string result in search)
			{
				Entity entity = Wikidata.GetEntity(result);

				if (!entity.HasClaim(MediaWiki.Wikidata.Prop_DateOfBirth)
					|| entity.GetClaimValueAsDate(MediaWiki.Wikidata.Prop_DateOfBirth).GetYear().ToString() != yearOfBirth)
				{
					continue;
				}
				if (!entity.HasClaim(MediaWiki.Wikidata.Prop_DateOfDeath)
					|| entity.GetClaimValueAsDate(MediaWiki.Wikidata.Prop_DateOfDeath).GetYear().ToString() != yearOfDeath)
				{
					continue;
				}

				foreach (string label in entity.labels.Values)
				{
					if (label == name)
					{
						return result;
					}
				}
				foreach (string[] labels in entity.aliases.Values)
				{
					foreach (string label in labels)
					{
						if (label == name)
						{
							return result;
						}
					}
				}
			}
			return "";
		}

		/// <summary>
		/// Parse the specified article and attempt to create a Creator based on it and the provided wikidata.
		/// </summary>
		/// <returns>Success</returns>
		private static bool ProcessCreatorCategory(Api commonsApi,
			Article article, string wikidataId)
		{
			string text = article.revisions[0].text;

			text = MediaWiki.WikiUtils.RemoveDuplicateCategories(text);

			Entity entity = Wikidata.GetEntity(wikidataId);
			if (entity != null && !entity.missing)
			{
				bool needsRefetch = false;

				// send as homecat
				if (!entity.HasClaim("P373"))
				{
					string artNameNoSpace = article.title.Substring("Category:".Length);
					Wikidata.CreateEntityClaim(entity, "P373", artNameNoSpace, "(BOT) propagating Commons homecat", true);
					needsRefetch = true;
				}

				// pull out existing authority control
				string existingAuthority;
				Dictionary<string, string> existingAuthDict = new Dictionary<string, string>();
				MediaWiki.WikiUtils.RemoveTemplate("Authority control", text, out existingAuthority);
				if (!string.IsNullOrEmpty(existingAuthority))
				{
					string authTrim = existingAuthority.Trim().Trim('{', '}');
					string[] authSplit = authTrim.Split('|');
					foreach (string pair in authSplit)
					{
						string[] pairSplit = pair.Split('=');
						if (pairSplit.Length == 2 && !string.IsNullOrEmpty(pairSplit[1]))
						{
							existingAuthDict[pairSplit[0].Trim()] = pairSplit[1].Trim();
						}
					}

					// populate it to wikidata
					if (existingAuthDict.Count > 0)
					{
						foreach (KeyValuePair<string, string> kvPair in existingAuthDict)
						{
							string prop = MediaWiki.Wikidata.GetPropertyIdForAuthority(kvPair.Key);
							if (!string.IsNullOrEmpty(prop) && !entity.HasClaim(prop))
							{
								string converted = MediaWiki.Wikidata.ConvertAuthorityFromCommonsToWikidata(kvPair.Key, kvPair.Value);
								if (!string.IsNullOrEmpty(converted))
								{
									Wikidata.CreateEntityClaim(entity, prop,
										converted, "(BOT) Propagate authority from category on Commons", true);
								}
							}
						}
					}
				}

				if (needsRefetch)
				{
					// HACK: lazy refetch...
					entity = Wikidata.GetEntity(wikidataId);
				}

				// get existing sortkey for creator
				string defaultSort = "{{DEFAULTSORT:";
				int sortkeyLoc = text.IndexOf(defaultSort);
				string sortkey = "";
				if (sortkeyLoc >= 0)
				{
					int sortkeyEnd = text.IndexOf("}}", sortkeyLoc);
					int sortkeyStart = sortkeyLoc + defaultSort.Length;
					sortkey = text.Substring(sortkeyStart, sortkeyEnd - sortkeyStart);
				}

				string creatorPage;
				TryMakeCreator(commonsApi, entity, sortkey, existingAuthDict, out creatorPage);

				// add creator template to page
				//TODO: try to maintain position
				Article creatorArticle = !string.IsNullOrEmpty(creatorPage)
					? commonsApi.GetPage(creatorPage)
					: null;
				if (creatorArticle != null && !creatorArticle.missing)
				{
					// remove 'creator possible'
					string eat;
					text = MediaWiki.WikiUtils.RemoveTemplate("Creator possible", text, out eat);

					// add creator template
					string creatorTemplate = "{{" + creatorArticle.title + "}}";
					//TODO: more validation
					if (!text.Contains("{{Creator:") && !text.Contains("{{creator:"))
					{
						text = creatorTemplate + "\n" + text;
					}

					// remove authority from cat and propagate to the creator
					//TODO:
					/*text =*/ MediaWiki.WikiUtils.RemoveTemplate("Authority control", text, out existingAuthority);

					// propagate existing creator to wikidata
					if (!entity.HasClaim("P1472"))
					{
						string creatorNameNoSpace = creatorArticle.title.Substring("Creator:".Length);
						Wikidata.CreateEntityClaim(entity, "P1472", creatorNameNoSpace, "(BOT) propagating Commons creator", true);
					}

					// remove On Wikidata template (redundant with creator)
					string oldWikidata;
					string textNoOnWikidata = MediaWiki.WikiUtils.RemoveTemplate("On Wikidata", text, out oldWikidata);
					if (oldWikidata == wikidataId)
					{
						text = textNoOnWikidata;
					}
					else if (!string.IsNullOrEmpty(oldWikidata))
					{
						text = MediaWiki.WikiUtils.AddCategory("Category:Categories with On Wikidata template that doesn't match Creator wikidata parameter", text);
					}

					// fix implicit creators in the category
					if (s_FixImplicitCreators)
					{
						Console.WriteLine("...checking child files");
						foreach (Article subArticle in commonsApi.GetCategoryPagesRecursive(article.title, 4))
						{
							if (subArticle.ns == MediaWiki.Namespace.File)
							{
								Article gotSubArticle = commonsApi.GetPage(subArticle);
								FixImplicitCreators.Do(commonsApi, gotSubArticle, creatorArticle.title);
								//PdOldAuto.Do(commonsApi, gotSubArticle);

								if (gotSubArticle.Dirty)
								{
									commonsApi.EditPage(gotSubArticle, gotSubArticle.GetEditSummary());
								}
							}
						}
					}
				}
				else
				{
					// add wikidata template to page
					//TODO:
				}

				// add appropriate categories if they don't exist
				text = MediaWiki.WikiUtils.AddCategory("Category:People by name", text);

				// gender cats
				if (entity.HasClaim("P21"))
				{
					string gender = entity.GetClaimValueAsGender("P21");
					if (gender == "male")
					{
						//TODO: use country subcats
						//text = Wikimedia.WikiUtils.AddCategory("Category:Men by name", text);
					}
					else if (gender == "female")
					{
						//TODO: use country subcats
						//text = Wikimedia.WikiUtils.AddCategory("Category:Women by name", text);
					}
				}

				// dead cat
				if (entity.HasClaim(MediaWiki.Wikidata.Prop_DateOfDeath))
				{
					MediaWiki.DateTime dateData = entity.GetClaimValueAsDate(MediaWiki.Wikidata.Prop_DateOfDeath);

					int year = dateData.GetYear();
					/*if (year < DateTime.Now.Year)
					{
						text = Wikimedia.WikiUtils.AddCategory("Category:Deceased persons by name", text);
					}*/

					string suffix = " deaths";
					string decade = dateData.GetString(MediaWiki.DateTime.DecadePrecision) + "s";
					string century = StringUtility.FormatOrdinal(dateData.GetCentury()) + "-century";

					bool hasYear = MediaWiki.WikiUtils.HasCategory(year + suffix, text);
					bool hasDecade = MediaWiki.WikiUtils.HasCategory(decade + suffix, text);

					if (dateData.Precision <= MediaWiki.DateTime.CenturyPrecision)
					{
						// never downgrade
						if (!hasYear && !hasDecade)
						{
							text = MediaWiki.WikiUtils.AddCategory(century + suffix, text);
						}
					}
					else if (dateData.Precision <= MediaWiki.DateTime.DecadePrecision)
					{
						// never downgrade
						if (!hasYear)
						{
							text = MediaWiki.WikiUtils.AddCategory(decade + suffix, text);
						}
					}
					else
					{
						text = MediaWiki.WikiUtils.AddCategory(year + suffix, text);
					}
				}

				// no death cat?
				MatchCollection deathMatch = Regex.Matches(text, "\\[\\[[Cc]ategory:.+ deaths[\\]\\|]");
				if (deathMatch.Count > 0)
				{
					text = MediaWiki.WikiUtils.RemoveCategory("Category:Year of death missing", text);
				}
				else
				{
					text = MediaWiki.WikiUtils.AddCategory("Category:Year of death missing", text);
				}

				// birth cat
				if (entity.HasClaim(MediaWiki.Wikidata.Prop_DateOfBirth))
				{
					MediaWiki.DateTime dateData = entity.GetClaimValueAsDate(MediaWiki.Wikidata.Prop_DateOfBirth);

					int year = dateData.GetYear();

					string suffix = " births";
					string decade = dateData.GetString(MediaWiki.DateTime.DecadePrecision) + "s";
					string century = StringUtility.FormatOrdinal(dateData.GetCentury()) + "-century";

					bool hasYear = MediaWiki.WikiUtils.HasCategory(year + suffix, text);
					bool hasDecade = MediaWiki.WikiUtils.HasCategory(decade + suffix, text);

					if (dateData.Precision <= MediaWiki.DateTime.CenturyPrecision)
					{
						// never downgrade
						if (!hasYear && !hasDecade)
						{
							text = MediaWiki.WikiUtils.AddCategory(century + suffix, text);
						}
					}
					else if (dateData.Precision <= MediaWiki.DateTime.DecadePrecision)
					{
						// never downgrade
						if (!hasYear)
						{
							text = MediaWiki.WikiUtils.AddCategory(decade + suffix, text);
						}
					}
					else
					{
						text = MediaWiki.WikiUtils.AddCategory(year + suffix, text);
					}
				}

				// no birth cat?
				MatchCollection birthMatch = Regex.Matches(text, "\\[\\[[Cc]ategory:.+ births[\\]\\|]");
				if (birthMatch.Count > 0)
				{
					text = MediaWiki.WikiUtils.RemoveCategory("Category:Year of birth missing", text);
				}
				else
				{
					text = MediaWiki.WikiUtils.AddCategory("Category:Year of birth missing", text);
				}

				if (birthMatch.Count > 1 || deathMatch.Count > 1)
				{
					text = MediaWiki.WikiUtils.AddCategory("Category:Categories with conflicting birth or death categories", text);
				}
				else
				{
					text = MediaWiki.WikiUtils.RemoveCategory("Category:Categories with conflicting birth or death categories", text);
				}

				// add occupation cats
				//TODO:

				// wikilinks
				/*foreach (KeyValuePair<string, string> kv in entity.sitelinks)
				{
					//HACK: length restriction
					if (kv.Key.EndsWith("wiki") && kv.Key.Length <= 9)
					{
						string key = kv.Key.Substring(0, kv.Key.Length - 4);
						text = Wikimedia.WikiUtils.AddInterwiki(key, kv.Value, text);
					}
				}*/

				// upload
				if (article.revisions[0].text != text)
				{
					article.revisions[0].text = text;
					if (!commonsApi.EditPage(article, "BOT: creator cleanup tasks"))
					{
						Console.WriteLine("Page set failed.");
						return false;
					}
					else
					{
						return true;
					}
				}
				else
				{
					Console.WriteLine("No changes.");
					return false;
				}
			}
			else
			{
				Console.WriteLine("Couldn't find wikidata entity.");
				return false;
			}
		}

		/// <summary>
		/// Creates creator templates from the wikidata ids in 'creator_queue.txt'.
		/// </summary>
		[BatchTask]
		public static void MakeCreators()
		{
			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org"));

			Console.WriteLine("Logging in...");
			commonsApi.AutoLogIn();
			Wikidata.AutoLogIn();

			using (StreamReader reader = new StreamReader(new FileStream("creator_queue.txt", FileMode.Open), Encoding.Default))
			{
				while (!reader.EndOfStream)
				{
					Entity entity = Wikidata.GetEntity(reader.ReadLine());
					string page;
					TryMakeCreator(commonsApi, entity, "", null, out page);
				}
			}
		}

		/// <summary>
		/// Tries to create a Creator template for the specified Wikimedia entity, on Commons.
		/// </summary>
		/// <param name="creatorPage">The name of the Creator page created.</param>
		/// <returns>True if the Creator page now exists.</returns>
		public static bool TryMakeCreator(Api commonsApi, Entity entity,
			string sortkey, Dictionary<string, string> extraAuthority,
			out string creatorPage)
		{
			if (!entity.labels.ContainsKey("en"))
			{
				Console.WriteLine("No English label.");
				creatorPage = "";
				return false;
			}
			if (!entity.HasClaim("P31") || entity.GetClaimValueAsEntityId("P31") != 5)
			{
				Console.WriteLine("Wikidata entity is not a person.");
				creatorPage = "";
				return false;
			}

			string name = entity.labels["en"];
			string commonsArticle = "Creator:" + name;

			Console.WriteLine("Attempting to create '" + commonsArticle + "' from wikidata.");

			//Check that it does not already exist on commons
			/*Wikimedia.Article existing = commonsApi.GetPage(commonsArticle);
			if (existing != null && existing.revisions != null && existing.revisions.Length > 0)
			{
				Console.WriteLine("Already exists.");
				return true;
			}*/

			//already has creator?
			if (entity.HasClaim("P1472"))
			{
				Console.WriteLine("Creator claim already exists.");
				creatorPage = "Creator:" + entity.GetClaimValueAsString("P1472");
				return true;
			}

			// creation disabled?
			if (!s_MakeNewCreators)
			{
				creatorPage = "";
				return false;
			}

			//get name(s)
			string langnames = "";
			foreach (KeyValuePair<string, string> kv in entity.labels)
			{
				if (entity.sitelinks.ContainsKey(kv.Key + "wiki"))
				{
					string sitelink = entity.sitelinks[kv.Key + "wiki"];
					langnames += "\n |" + kv.Key + "=[[:" + kv.Key + ":" + kv.Value + "|" + sitelink + "]]";
				}
				else
					langnames += "\n |" + kv.Key + "=" + kv.Value;
			}

			//get nationality
			string nationality = "";
			if (entity.HasClaim("P27"))
			{
				Entity nationalityEntity = entity.GetClaimValueAsEntity("P27", Wikidata);
				if (nationalityEntity.HasClaim("P297"))
					nationality = nationalityEntity.GetClaimValueAsString("P297");
			}

			//get gender
			string gender = "";
			if (entity.HasClaim("P21"))
			{
				gender = entity.GetClaimValueAsGender("P21");
			}

			//get occupation
			string occupation = "";
			if (entity.HasClaim("P106"))
			{
				foreach (Entity occupationEntity in entity.GetClaimValuesAsEntity("P106", Wikidata))
				{
					occupation += occupationEntity.labels["en"] + "/";
				}
				if (occupation.Length > 0)
				{
					occupation = occupation.Remove(occupation.Length - 1);
				}
			}

			//birth/death dates
			string birthdate = "";
			if (entity.HasClaim("P569"))
			{
				birthdate = entity.GetClaimValueAsDate("P569").GetString(MediaWiki.DateTime.DayPrecision);
			}
			string deathdate = "";
			if (entity.HasClaim("P570"))
			{
				deathdate = entity.GetClaimValueAsDate("P570").GetString(MediaWiki.DateTime.DayPrecision);
			}

			//birth/death locs
			string birthloc = "";
			if (entity.HasClaim("P19"))
			{
				Entity locEntity = entity.GetClaimValueAsEntity("P19", Wikidata);
				birthloc = locEntity.labels["en"];
			}
			string deathloc = "";
			if (entity.HasClaim("P20"))
			{
				Entity locEntity = entity.GetClaimValueAsEntity("P20", Wikidata);
				deathloc = locEntity.labels["en"];
			}

			// working
			string workloc = "";
			if (entity.HasClaim("P937"))
			{
				Entity locEntity = entity.GetClaimValueAsEntity("P937", Wikidata);
				workloc = locEntity.labels["en"];
			}
			string workperiod = "";
			if (entity.HasClaim("P2031"))
			{
				workperiod = entity.GetClaimValueAsDate("P2031").GetString(MediaWiki.DateTime.DayPrecision);
				if (entity.HasClaim("P2032"))
				{
					workperiod = "{{other date|-|" + workperiod + "|"
						+ entity.GetClaimValueAsDate("P2032").GetString(MediaWiki.DateTime.DayPrecision) +"}}";
				}
			}

			string homecat = name;
			if (entity.HasClaim("P373"))
			{
				homecat = entity.GetClaimValueAsString("P373");
			}

			//authority control
			string authority = MediaWiki.Wikidata.GetAuthorityControlTemplate(entity, "bare=1", extraAuthority);

			Article creatorArt = new Article();
			creatorArt.title = commonsArticle;
			creatorArt.revisions = new Revision[1];
			creatorArt.revisions[0] = new Revision();
			creatorArt.revisions[0].text = "{{Creator" +
				"\n|Image=" +
				"\n|Name={{LangSwitch" + langnames +
				"\n |default=" + name +
				"\n}}" +
				"\n|Alternative names=" +
				"\n|Nationality=" + nationality +
				"\n|Gender=" + gender +
				"\n|Occupation=" + occupation +
				"\n|Description=" + //(entity.descriptions.ContainsKey("en") ? entity.descriptions["en"] : "") +
				"\n|Birthdate=" + birthdate +
				"\n|Birthloc=" + birthloc +
				"\n|Deathdate=" + deathdate +
				"\n|Deathloc=" + deathloc +
				"\n|Workperiod=" + workperiod +
				"\n|Workloc=" + workloc +
				"\n|Homecat=" + homecat +
				"\n|Linkback=" + commonsArticle +
				"\n|Option={{{1|}}}" +
				"\n|Sortkey=" + sortkey +
				"\n|Authority=" + authority +
				"\n|Wikidata=" + entity.id +
				"\n}}";

			Wikidata.CreateEntityClaim(entity, "P1472", name, "(BOT) creating Commons creator page", true);

			//Do not overwrite if it exists
			Article safety = commonsApi.GetPage(creatorArt);
			if (safety != null && !safety.missing)
			{
				Console.WriteLine("Creator page already exists!");
				creatorPage = safety.title;
				return true;
			}

			if (commonsApi.CreatePage(creatorArt, "BOT: Making creator based on Wikidata."))
			{
				Console.WriteLine("Creator created!");
				creatorPage = creatorArt.title;
				return true;
			}
			else
			{
				Console.WriteLine("Failed to create page.");
				creatorPage = "";
				return false;
			}
		}

		/// <summary>
		/// Fixes formatting issues with Information/Artwork/Book templates.
		/// </summary>
		/// <param name="article">The already-downloaded article.</param>
		public static void FixInformationTemplates(Article article, HashSet<string> removeEmptyParams = null)
		{
			if (MediaWiki.Article.IsNullOrEmpty(article))
			{
				Console.WriteLine("FixInformationTemplates: FATAL: article missing");
				return;
			}

			string text = article.revisions[0].text;
			bool mainTemplate = false;
			bool artTemplate = false;

			// set if either "artist" or "author" exists and is filled
			bool artTemplateHasFilledAuthor = false;

			bool nowiki = false;
			bool comment = false;
			Stack<string> openTemplates = new Stack<string>();
			int templateStartIndex = -1;
			int paramNameStartIndex = -1;
			bool hasError = false;
			for (int c = 0; c < text.Length - 1; c++)
			{
				if (StringAt(text, "<!--", c))
				{
					comment = true;
				}
				else if (StringAt(text, "-->", c))
				{
					comment = false;
				}
				if (!comment)
				{
					if (StringAt(text, "<nowiki>", c))
					{
						nowiki = true;
					}
					else if (StringAt(text, "</nowiki>", c))
					{
						nowiki = false;
					}
					if (!nowiki)
					{
						if (StringAt(text, "[[", c))
						{
							templateStartIndex = c;
							paramNameStartIndex = -1;
							c += 1;
						}
						else if (StringAt(text, "]]", c))
						{
							if (templateStartIndex >= 0)
							{
								templateStartIndex = -1;
							}
							else
							{
								if (openTemplates.Count == 0)
								{
									hasError = true;
								}
								else
								{
									openTemplates.Pop();
								}
								mainTemplate = openTemplates.Count > 0 && IsMainTemplate(openTemplates.Peek());
								artTemplate = openTemplates.Count > 0 && IsArtTemplate(openTemplates.Peek());
							}
							paramNameStartIndex = -1;
							c += 1;
						}
						else if (StringAt(text, "{{", c))
						{
							templateStartIndex = c;
							paramNameStartIndex = -1;
							c += 1;
						}
						else if (StringAt(text, "}}", c))
						{
							if (templateStartIndex >= 0)
							{
								templateStartIndex = -1;
							}
							else
							{
								if (openTemplates.Count == 0)
								{
									hasError = true;
								}
								else
								{
									openTemplates.Pop();
								}
								mainTemplate = openTemplates.Count > 0 && IsMainTemplate(openTemplates.Peek());
								artTemplate = openTemplates.Count > 0 && IsArtTemplate(openTemplates.Peek());
							}
							paramNameStartIndex = -1;
							c += 1;
						}
						if (templateStartIndex >= 0 && text[c] == '|')
						{
							openTemplates.Push(text.Substring(templateStartIndex, c - templateStartIndex).Trim());
							mainTemplate = openTemplates.Count > 0 && IsMainTemplate(openTemplates.Peek());
							artTemplate = openTemplates.Count > 0 && IsArtTemplate(openTemplates.Peek());
							templateStartIndex = -1;
						}
						else if (mainTemplate && text[c] == '|')
						{
							// force line returns before every main template param

							//1. backtrack through previous non-newline whitespace
							bool alreadyHasNewline = false;
							int backtrack = c - 1;
							for (; backtrack >= 0; backtrack--)
							{
								if (text[backtrack] == '\n')
								{
									alreadyHasNewline = true;
									break;
								}
								else if (!char.IsWhiteSpace(text[backtrack]))
								{
									backtrack++;
									break;
								}
							}

							//2. place a newline
							if (!alreadyHasNewline)
							{
								text = text.Substring(0, backtrack) + "\n" + text.Substring(backtrack, text.Length - backtrack);
								c++;
							}

							paramNameStartIndex = c + 1;
						}

						if (text[c] == '=' && paramNameStartIndex >= 0)
						{
							string paramName = text.Substring(paramNameStartIndex, c - paramNameStartIndex).Trim();

							for (int lookahead = c + 1; lookahead < text.Length; lookahead++)
							{
								if (!char.IsWhiteSpace(text[lookahead]))
								{
									if (text[lookahead] == '|' || StringAt(text, "}}", lookahead))
									{
										//empty parameter
										if (removeEmptyParams != null && removeEmptyParams.Contains(paramName))
										{
											// remove it!
											text = text.Substring(0, paramNameStartIndex - 1) + text.Substring(lookahead, text.Length - lookahead);
											c = paramNameStartIndex - 1;
										}
									}
									else
									{
										if (paramName == "author" || paramName == "artist")
										{
											artTemplateHasFilledAuthor = true;
										}
									}
									break;
								}
							}

							paramNameStartIndex = -1;
						}
					}
				}
			}

			if (hasError)
			{
				text = MediaWiki.WikiUtils.AddCategory("Category:Pages with mismatched parentheses", text);
			}
			else if (artTemplateHasFilledAuthor && removeEmptyParams == null)
			{
				FixInformationTemplates(article, artistOrAuthor);
			}

			if (text != article.revisions[0].text)
			{
				article.revisions[0].text = text;
				article.Dirty = true;
			}
		}

		private static HashSet<string> artistOrAuthor = new HashSet<string> { "artist", "author" };

		private static bool IsMainTemplate(string template)
		{
			template = template.TrimStart('{');
			return string.Compare(template, "information") == 0
				|| string.Compare(template, "Information") == 0
				|| IsArtTemplate(template)
				|| string.Compare(template, "book") == 0
				|| string.Compare(template, "Book") == 0
				|| string.Compare(template, "photograph") == 0
				|| string.Compare(template, "Photograph") == 0
				|| string.Compare(template, "google Art Project") == 0
				|| string.Compare(template, "Google Art Project") == 0;
		}

		private static bool IsArtTemplate(string template)
		{
			return string.Compare(template, "artwork") == 0
				|| string.Compare(template, "Artwork") == 0;
		}

		/// <summary>
		/// Returns true if 'search' occurs in 'text' at location 'index'.
		/// </summary>
		private static bool StringAt(string text, string search, int index)
		{
			if (text.Length - index < search.Length) return false;
			for (int c = 0; c < search.Length; c++)
			{
				if (text[c + index] != search[c])
				{
					return false;
				}
			}
			return true;
		}
	}
}
