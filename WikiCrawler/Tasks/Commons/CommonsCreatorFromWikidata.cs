using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WikiCrawler;

namespace Tasks.Commons
{
	/// <summary>
	/// Creates creator templates from the wikidata ids in 'creator_queue.txt'.
	/// </summary>
	public class MakeCreatorsFromList : BaseTask
	{
		public override void Execute()
		{
			using (StreamReader reader = new StreamReader(new FileStream("creator_queue.txt", FileMode.Open), Encoding.Default))
			{
				while (!reader.EndOfStream)
				{
					QId qid = QId.Parse(reader.ReadLine());
					Entity entity = GlobalAPIs.Wikidata.GetEntity(qid);
					PageTitle page;
					CommonsCreatorFromWikidata.TryMakeCreator(entity, out page);
				}
			}
		}
	}

	/// <summary>
	/// Creates creator templates from 'Category:Creator templates to be created by a bot'.
	/// </summary>
	public class CommonsCreatorFromWikidata : BaseTask
	{
		//TODO: support BC dates

		private static readonly bool s_DoBotCat = false;
		private static readonly bool s_DoNormalCat = true;
		private static readonly int s_TestLimit = int.MaxValue;
		private static readonly bool s_MakeNewCreators = true;
		private static readonly bool s_FixImplicitCreators = true;

		public override void Execute()
		{
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
					foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(new PageTitle(PageTitle.NS_Category, "Creator templates to be created by a bot"), cmtype: CMType.subcat))
					{
						Console.WriteLine("Checking verified '" + article.title + "'...");

						if (!article.title.IsNamespace(PageTitle.NS_Category))
						{
							Console.WriteLine("Error: not a category.");
							continue;
						}

						Article articleContent = GlobalAPIs.Commons.GetPage(article);
						CommonsCreatorWorksheet worksheet = new CommonsCreatorWorksheet(articleContent);

						Console.WriteLine("Wikidata Id is '{0}'.", worksheet.Wikidata);
						QId qid = QId.SafeParse(worksheet.Wikidata);

						if (qid.IsEmpty)
						{
							//TODO: error
						}
						else if (ProcessCreatorCategory(articleContent, qid))
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
					foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(new PageTitle(PageTitle.NS_Category, "People by name"), cmtype: CMType.subcat, cmstartsortkeyprefix: normalCatLastPage))
					{
						Console.WriteLine("----- Checking '" + article.title + "'...");

						if (!article.title.IsNamespace(PageTitle.NS_Category))
						{
							Console.WriteLine("Error: not a category.");
							continue;
						}

						Article articleContent = GlobalAPIs.Commons.GetPage(article);
						string text = articleContent.revisions[0].text;

						QId wikidataId = QId.Empty;

						// check creator for a wikidata id
						if (wikidataId.IsEmpty)
						{
							string creatorPre = "{{Creator:";
							int creatorIndex = text.IndexOf(creatorPre);
							if (creatorIndex >= 0)
							{
								int creatorEnd = WikiUtils.GetTemplateEnd(text, creatorIndex);
								string creator = text.SubstringRange(creatorIndex + 2, creatorEnd - 2);
								PageTitle creatorPage = PageTitle.Parse(creator);

								CreatorData creatorData = WikidataCache.GetCreatorData(creatorPage);
								if (creatorData != null)
								{
									wikidataId = creatorData.QID;
								}
							}
						}

						// check for On Wikidata
						if (wikidataId.IsEmpty)
						{
							string onWikidataTemplate = WikiUtils.ExtractTemplate("On Wikidata", text);
							if (!string.IsNullOrEmpty(onWikidataTemplate))
							{
								string idString = WikiUtils.GetTemplateParameter(1, onWikidataTemplate);
								wikidataId = QId.SafeParse(idString);
								if (!wikidataId.IsEmpty)
								{
									Console.WriteLine("Matched ON WIKIDATA");
								}
							}
						}

						if (wikidataId.IsEmpty)
						{
							// look for DOB and DOD categories
							string yearOfBirth = "";
							string yearOfDeath = "";
							foreach (PageTitle cat in WikiUtils.GetCategories(text))
							{
								//HACK: only works with 4-digit years
								if (cat.Name.EndsWith(" births"))
								{
									yearOfBirth = cat.Name.Substring(0, 4);
								}
								else if (cat.Name.EndsWith(" deaths"))
								{
									yearOfDeath = cat.Name.Substring(0, 4);
								}
							}

							if (string.IsNullOrEmpty(yearOfBirth) || string.IsNullOrEmpty(yearOfDeath))
							{
								Console.WriteLine("Insufficient information.");
							}
							else
							{
								Entity wikidata = GetWikidata(article.title.Name, yearOfBirth, yearOfDeath);
								if (!Entity.IsNullOrMissing(wikidata))
								{
									wikidataId = wikidata.id;
									Console.WriteLine("Got wikidata from Creator");
								}
							}
						}

						if (!wikidataId.IsEmpty)
						{
							Console.WriteLine("++ Matched " + wikidataId + ".");

							if (ProcessCreatorCategory(articleContent, wikidataId))
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

						normalCatLastPage = WikiUtils.GetSortkey(article);
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

		private struct WikidataSearch
		{
			public string Name;
			public string YearOfBirth;
			public string YearOfDeath;

			public override bool Equals(object obj)
			{
				if (obj is WikidataSearch)
				{
					WikidataSearch search = (WikidataSearch)obj;
					return search.Name == Name && search.YearOfBirth == YearOfBirth && search.YearOfDeath == YearOfDeath;
				}
				else
				{
					return false;
				}
			}

			public override int GetHashCode()
			{
				int hashCode = -1656821439;
				hashCode = hashCode * -1521134295 + Name.GetHashCode();
				hashCode = hashCode * -1521134295 + YearOfBirth.GetHashCode();
				hashCode = hashCode * -1521134295 + YearOfDeath.GetHashCode();
				return hashCode;
			}
		}

		private static Dictionary<WikidataSearch, Entity> s_searchCache = new Dictionary<WikidataSearch, Entity>();

		/// <summary>
		/// Searches for a wikidata entity matching the specified info.
		/// </summary>
		public static Entity GetWikidata(string name, string yearOfBirth, string yearOfDeath)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			WikidataSearch search = new WikidataSearch
			{
				Name = name,
				YearOfBirth = yearOfBirth,
				YearOfDeath = yearOfDeath
			};
			if (s_searchCache.TryGetValue(search, out Entity cachedEntity))
			{
				return cachedEntity;
			}

			Entity entity = GetWikidataUncached(name, yearOfBirth, yearOfDeath);
			s_searchCache[search] = entity;
			return entity;
		}

		public static Entity GetWikidataUncached(string name, string yearOfBirth, string yearOfDeath)
		{
			int yob = int.Parse(yearOfBirth);
			int yod = int.Parse(yearOfDeath);

			// search wikidata
			IEnumerable<QId> search = GlobalAPIs.Wikidata.SearchEntities(name);
			foreach (Entity entity in GlobalAPIs.Wikidata.GetEntities(search.ToArray()))
			{
				if (!entity.HasClaim(Wikidata.Prop_DateOfBirth)
					|| !entity.GetClaimValuesAsDates(Wikidata.Prop_DateOfBirth).Any(date => date.Precision >= MediaWiki.DateTime.YearPrecision && date.GetYear() == yob))
				{
					continue;
				}
				if (!entity.HasClaim(Wikidata.Prop_DateOfDeath)
					|| !entity.GetClaimValuesAsDates(Wikidata.Prop_DateOfDeath).Any(date => date.Precision >= MediaWiki.DateTime.YearPrecision && date.GetYear() == yod))
				{
					continue;
				}

				foreach (string label in entity.labels.Values)
				{
					if (label == name)
					{
						return entity;
					}
				}
				foreach (string[] labels in entity.aliases.Values)
				{
					foreach (string label in labels)
					{
						if (label == name)
						{
							return entity;
						}
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Parse the specified article and attempt to create a Creator based on it and the provided wikidata.
		/// </summary>
		/// <returns>True on success</returns>
		private static bool ProcessCreatorCategory(Article article, QId wikidataId)
		{
			string text = article.revisions[0].text;

			text = WikiUtils.RemoveDuplicateCategories(text);

			Entity entity = GlobalAPIs.Wikidata.GetEntity(wikidataId);
			if (entity != null && !entity.missing)
			{
				bool needsRefetch = false;

				// send as homecat
				if (!entity.HasClaim("P373"))
				{
					string artNameNoSpace = article.title.Name;
					GlobalAPIs.Wikidata.CreateEntityClaim(entity, "P373", artNameNoSpace, "(BOT) propagating Commons homecat", true);
					needsRefetch = true;
				}

				// pull out existing authority control
				string existingAuthority;
				Dictionary<string, string> existingAuthDict = new Dictionary<string, string>();
				WikiUtils.RemoveTemplate("Authority control", text, out existingAuthority);
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
							string prop = Wikidata.GetPropertyIdForAuthority(kvPair.Key);
							if (!string.IsNullOrEmpty(prop) && !entity.HasClaim(prop))
							{
								string converted = Wikidata.ConvertAuthorityFromCommonsToWikidata(kvPair.Key, kvPair.Value);
								if (!string.IsNullOrEmpty(converted))
								{
									GlobalAPIs.Wikidata.CreateEntityClaim(entity, prop,
										converted, "(BOT) Propagate authority from category on Commons", true);
								}
							}
						}
					}
				}

				if (needsRefetch)
				{
					// HACK: lazy refetch...
					entity = GlobalAPIs.Wikidata.GetEntity(wikidataId);
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

				PageTitle creatorPage;
				TryMakeCreator(entity, out creatorPage);

				// add creator template to page
				//TODO: try to maintain position
				Article creatorArticle = !creatorPage.IsEmpty
					? GlobalAPIs.Commons.GetPage(creatorPage)
					: null;
				if (creatorArticle != null && !creatorArticle.missing)
				{
					// remove 'creator possible'
					string eat;
					text = WikiUtils.RemoveTemplate("Creator possible", text, out eat);

					// add creator template
					string creatorTemplate = "{{" + creatorArticle.title + "}}";
					//TODO: more validation
					if (!text.Contains("{{Creator:") && !text.Contains("{{creator:"))
					{
						text = creatorTemplate + "\n" + text;
					}

					// remove authority from cat and propagate to the creator
					//TODO:
					/*text =*/ WikiUtils.RemoveTemplate("Authority control", text, out existingAuthority);

					// propagate existing creator to wikidata
					if (!entity.HasClaim(Wikidata.Prop_CommonsCreator))
					{
						string creatorNameNoSpace = creatorArticle.title.Name;
						GlobalAPIs.Wikidata.CreateEntityClaim(entity, Wikidata.Prop_CommonsCreator, creatorNameNoSpace, "(BOT) propagating Commons creator", true);
					}

					// remove On Wikidata template (redundant with creator)
					string textNoOnWikidata = WikiUtils.RemoveTemplate("On Wikidata", text, out string onWikidata);
					string onWikidataQid = WikiUtils.GetTemplateParameter(1, onWikidata);
					if (QId.Parse(onWikidataQid) == wikidataId)
					{
						text = textNoOnWikidata;
					}
					else if (!string.IsNullOrEmpty(onWikidata))
					{
						text = WikiUtils.AddCategory("Category:Categories with On Wikidata template that doesn't match Creator wikidata parameter", text);
					}

					// fix implicit creators in the category
					if (s_FixImplicitCreators)
					{
						Console.WriteLine("...checking child files");
						IEnumerable<Article> subArticles = GlobalAPIs.Commons.GetCategoryEntriesRecursive(article.title, 4, cmtype: CMType.file);
						foreach (Article subArticle in GlobalAPIs.Commons.FetchArticles(subArticles))
						{
							if (subArticle.ns == Namespace.File)
							{
								//TODO: new ImplicitCreatorsReplacement().DoReplacement(gotSubArticle, PageTitle.Parse(creatorArticle.title));
								//PdOldAuto.Do(commonsApi, gotSubArticle);

								if (subArticle.Dirty)
								{
									GlobalAPIs.Commons.EditPage(subArticle, subArticle.GetEditSummary());
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
				text = WikiUtils.AddCategory("Category:People by name", text);

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
				if (entity.HasClaim(Wikidata.Prop_DateOfDeath))
				{
					//HACK: use first only
					MediaWiki.DateTime dateData = entity.GetClaimValuesAsDates(Wikidata.Prop_DateOfDeath).First();

					int year = dateData.GetYear();
					/*if (year < DateTime.Now.Year)
					{
						text = Wikimedia.WikiUtils.AddCategory("Category:Deceased persons by name", text);
					}*/

					string suffix = " deaths";
					string decade = dateData.GetString(MediaWiki.DateTime.DecadePrecision) + "s";
					string century = StringUtility.FormatOrdinal(dateData.GetCentury()) + "-century";

					bool hasYear = WikiUtils.HasCategory(new PageTitle(PageTitle.NS_Category, year + suffix), text);
					bool hasDecade = WikiUtils.HasCategory(new PageTitle(PageTitle.NS_Category, decade + suffix), text);

					if (dateData.Precision <= MediaWiki.DateTime.CenturyPrecision)
					{
						// never downgrade
						if (!hasYear && !hasDecade)
						{
							text = WikiUtils.AddCategory(century + suffix, text);
						}
					}
					else if (dateData.Precision <= MediaWiki.DateTime.DecadePrecision)
					{
						// never downgrade
						if (!hasYear)
						{
							text = WikiUtils.AddCategory(decade + suffix, text);
						}
					}
					else
					{
						text = WikiUtils.AddCategory(year + suffix, text);
					}
				}

				// no death cat?
				MatchCollection deathMatch = Regex.Matches(text, "\\[\\[[Cc]ategory:.+ deaths[\\]\\|]");
				if (deathMatch.Count > 0)
				{
					text = WikiUtils.RemoveCategory("Category:Year of death missing", text);
				}
				else
				{
					text = WikiUtils.AddCategory("Category:Year of death missing", text);
				}

				// birth cat
				if (entity.HasClaim(Wikidata.Prop_DateOfBirth))
				{
					//HACK: use first only
					MediaWiki.DateTime dateData = entity.GetClaimValuesAsDates(Wikidata.Prop_DateOfBirth).First();

					int year = dateData.GetYear();

					string suffix = " births";
					string decade = dateData.GetString(MediaWiki.DateTime.DecadePrecision) + "s";
					string century = StringUtility.FormatOrdinal(dateData.GetCentury()) + "-century";

					bool hasYear = WikiUtils.HasCategory(new PageTitle(PageTitle.NS_Category, year + suffix), text);
					bool hasDecade = WikiUtils.HasCategory(new PageTitle(PageTitle.NS_Category, decade + suffix), text);

					if (dateData.Precision <= MediaWiki.DateTime.CenturyPrecision)
					{
						// never downgrade
						if (!hasYear && !hasDecade)
						{
							text = WikiUtils.AddCategory(century + suffix, text);
						}
					}
					else if (dateData.Precision <= MediaWiki.DateTime.DecadePrecision)
					{
						// never downgrade
						if (!hasYear)
						{
							text = WikiUtils.AddCategory(decade + suffix, text);
						}
					}
					else
					{
						text = WikiUtils.AddCategory(year + suffix, text);
					}
				}

				// no birth cat?
				MatchCollection birthMatch = Regex.Matches(text, "\\[\\[[Cc]ategory:.+ births[\\]\\|]");
				if (birthMatch.Count > 0)
				{
					text = WikiUtils.RemoveCategory("Category:Year of birth missing", text);
				}
				else
				{
					text = WikiUtils.AddCategory("Category:Year of birth missing", text);
				}

				if (birthMatch.Count > 1 || deathMatch.Count > 1)
				{
					text = WikiUtils.AddCategory("Category:Categories with conflicting birth or death categories", text);
				}
				else
				{
					text = WikiUtils.RemoveCategory("Category:Categories with conflicting birth or death categories", text);
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
					if (!GlobalAPIs.Commons.EditPage(article, "BOT: creator cleanup tasks"))
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
		/// Tries to create a Creator template for the specified Wikimedia entity, on Commons.
		/// </summary>
		/// <param name="creatorPage">The name of the Creator page created.</param>
		/// <returns>True if the Creator page now exists.</returns>
		public static bool TryMakeCreator(Entity entity, out PageTitle creatorPage)
		{
			Console.WriteLine("Attempting to create creator from wikidata '{0}'...", entity.id);

			if (!entity.labels.ContainsKey("en"))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Entity {0} has no English label.", entity.id);
				creatorPage = PageTitle.Empty;
				return false;
			}
			if (!entity.HasClaim(Wikidata.Prop_InstanceOf) || entity.GetClaimValueAsEntityId(Wikidata.Prop_InstanceOf) != Wikidata.Entity_Human)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Entity '{0}' is not a person.", entity.labels["en"]);
				creatorPage = PageTitle.Empty;
				return false;
			}

			// already has creator?
			if (entity.TryGetClaimValueAsString(Wikidata.Prop_CommonsCreator, out string[] creator))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Entity '{0}' has creator.", creator[0]);
				creatorPage = new PageTitle(PageTitle.NS_Creator, creator[0]);
				return true;
			}

			string name = entity.labels["en"];
			PageTitle commonsArticle = new PageTitle(PageTitle.NS_Creator, name);

			//FIXME
			if (name.Contains("\""))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Name has quotes.");
				creatorPage = PageTitle.Empty;
				return false;
			}

			// Check that it does not already exist on commons
			Article existing = GlobalAPIs.Commons.GetPage(commonsArticle);
			if (!Article.IsNullOrMissing(existing))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Creator page '{0}' already exists.", existing.title);
				creatorPage = PageTitle.Empty;
				return false;
			}

			// creation disabled?
			if (!s_MakeNewCreators)
			{
				creatorPage = PageTitle.Empty;
				return false;
			}

			Article creatorArt = new Article();
			creatorArt.title = commonsArticle;
			creatorArt.revisions = new Revision[1];
			creatorArt.revisions[0] = new Revision();
			creatorArt.revisions[0].text = "{{Creator" +
				"\n|Option={{{1|}}}" +
				"\n|Wikidata=" + entity.id +
				"\n}}";

			// goes first, or Commons page will need a cache purge
			GlobalAPIs.Wikidata.CreateEntityClaim(entity, Wikidata.Prop_CommonsCreator, name, "(BOT) creating Commons creator page", true);

			if (GlobalAPIs.Commons.CreatePage(creatorArt, "creating Creator template from Wikidata"))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Green, "  Created page '{0}'.", creatorArt.title);
				creatorPage = commonsArticle;
				return true;
			}
			else
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to create '{0}'.", creatorArt.title);
				creatorPage = PageTitle.Empty;
				return false;
			}
		}
	}
}
