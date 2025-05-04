using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WikiCrawler;

namespace Tasks.Commons
{
	public class MappingCreator : MappingValue
	{
		public string MappedValue;
		public string MappedQID;
		public int MappedDeathyear = 9999;
	}

	/// <summary>
	/// Replaces author strings with verified Creator templates.
	/// </summary>
	public class ImplicitCreatorsReplacement : BaseReplacement
	{
		public override bool UseHeartbeat
		{
			get { return true; }
		}

		/// <summary>
		/// If set, will walk up categories to try to map creator string.
		/// </summary>
		public static bool SlowCategoryWalk = true;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public readonly string ProjectDataDirectory;

		/// <summary>
		/// Caches the qids of entities linked to each category.
		/// </summary>
		//TODO: only store entities that are actually saved in s_Entities
		private static Dictionary<PageTitle, List<string>> s_CategoriesToEntities = new Dictionary<PageTitle, List<string>>();

		/// <summary>
		/// Caches entities by qid.
		/// </summary>
		private static Dictionary<string, Entity> s_Entities = new Dictionary<string, Entity>();

		/// <summary>
		/// Set of categories that have been checked for entities so far.
		/// </summary>
		private static HashSet<PageTitle> s_VisitedCats = new HashSet<PageTitle>();

		/// <summary>
		/// Set of pages that are known to exist.
		/// </summary>
		private static HashSet<PageTitle> s_ExtantPages = new HashSet<PageTitle>();

		private ManualMapping<MappingCreator> m_creatorMappings;

		public string CreatorMappingFile
		{
			get { return GetCreatorMappingFile(ProjectDataDirectory); }
		}

		public static string GetCreatorMappingFile(string projectDataDirectory)
		{
			return Path.Combine(projectDataDirectory, "creator-mappings.txt");
		}

		public static bool IsUnknownOrAnonymousAuthor(string author)
		{
			return IsUnknownAuthor(author) || IsAnonymousAuthor(author);
		}

		private static bool IsConvertibleUnknownAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "unknown", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocido", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocida", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "sconosciuto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "non noto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "non identifié", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "author unknown", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown}}", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsConvertibleUnknownArtist(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "Artist unknown", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "Unbekannter Maler", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsConvertibleAnonymousAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "anonymous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymouse", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "anonymous artist", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "anonymos artist", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonme", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "anonym", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "anon", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "auteur anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonimous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonimus", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymus", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "auteur anonyme", StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool IsUnknownAuthor(string author)
		{
			return IsConvertibleUnknownAuthor(author)
				|| string.Equals(author, "{{unknown|author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown|artist}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown|1=author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown|1=artist}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{author|unknown}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown photographer}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown|photographer}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{creator:unknown}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{creator:?}}", StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool IsAnonymousAuthor(string author)
		{
			string onlyTemplateName = WikiUtils.GetOnlyTemplateName(author);

			return IsConvertibleAnonymousAuthor(author)
				|| string.Equals(onlyTemplateName, "anonymous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anonymous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anon", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymous plate", StringComparison.InvariantCultureIgnoreCase); // don't know what to replace this with
		}

		private enum CreatorReplaceType
		{
			Implicit,
			Inline,
			RemoveLifespan,
		}

		public ImplicitCreatorsReplacement(string directory)
		{
			ProjectDataDirectory = Path.Combine(Configuration.DataDirectory, directory);
			Directory.CreateDirectory(ProjectDataDirectory);
			m_creatorMappings = new ManualMapping<MappingCreator>(CreatorMappingFile);
		}

		public override void SaveOut()
		{
			base.SaveOut();

			m_creatorMappings.Serialize();
		}

		/// <summary>
		/// Replace any verifiable implicit creators with creator templates.
		/// </summary>
		/// <param name="article">Article, already downloaded.</param>
		/// <returns>True if a replacement was made.</returns>
		public override bool DoReplacement(Article article)
		{
			return DoReplacement(article, null);
		}

		public bool DoReplacement(Article article, Entity suggestedCreator)
		{
			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (string.IsNullOrEmpty(worksheet.Author))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to find author.");
				return false;
			}

			Console.WriteLine("  Author string is '{0}'.", worksheet.Author);

			string newAuthor = "";
			CreatorReplaceType replaceType = CreatorReplaceType.Implicit;

			if (suggestedCreator != null)
			{
				string suggestedLabel = suggestedCreator.labels["en"];
				if (AuthorIs(worksheet.Author, suggestedCreator))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Green, "  Suggested creator '{0}' is a match.", suggestedLabel);

					newAuthor = "{{Creator:" + suggestedCreator.GetClaimValueAsString(Wikidata.Prop_CommonsCreator) + "}}";
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "  Suggested creator '{0}' is not a match.", suggestedLabel);
					return false;
				}
			}
			else if (IsConvertibleUnknownAuthor(worksheet.Author))
			{
				if (string.Equals(worksheet.AuthorParam, "artist") || string.Equals(worksheet.AuthorParam, "artist_display_name"))
				{
					newAuthor = "{{unknown|artist}}";
				}
				else if (string.Equals(worksheet.AuthorParam, "photographer"))
				{
					newAuthor = "{{unknown photographer}}";
				}
				else
				{
					newAuthor = "{{unknown|author}}";
				}
			}
			else if (IsConvertibleAnonymousAuthor(worksheet.Author))
			{
				newAuthor = "{{anonymous}}";
			}
			else if (IsConvertibleUnknownArtist(worksheet.Author))
			{
				newAuthor = "{{unknown|artist}}";
			}
			else if (IsUnknownAuthor(worksheet.Author) || IsAnonymousAuthor(worksheet.Author))
			{
				// already a template - do nothing
				ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "  Already a template");
				return false;
			}
			else if (CreatorUtility.TryGetCreatorTemplate(worksheet.Author, out PageTitle creatorTemplate))
			{
				if (GetPageExists(creatorTemplate))
				{
					// already a creator - do nothing
					ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "  Already a creator");

					//HACK: make sure this file is not still in the creator mappings
					foreach (var kv in m_creatorMappings)
					{
						kv.Value.FromPages.Remove(article.title);
						m_creatorMappings.SetDirty();
					}

					return false;
				}
				else
				{
					// already a creator, but a missing one
					ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Author is redlink creator '{0}'", creatorTemplate);
				}
			}
			else if (CreatorUtility.InlineCreatorTemplateRegex.MatchOut(worksheet.Author, out Match inlineCreatorMatch))
			{
				Entity entity = GlobalAPIs.Wikidata.GetEntity(inlineCreatorMatch.Groups[1].Value); //TODO: use cache
				if (!Entity.IsNullOrMissing(entity))
				{
					PageTitle creator;
					CommonsCreatorFromWikidata.TryMakeCreator(entity, out creator);
					newAuthor = "{{" + creator + "}}";
					replaceType = CreatorReplaceType.Inline;
				}
			}
			
			if (string.IsNullOrEmpty(newAuthor))
			{
				PageTitle creator = PageTitle.Empty;
				string authorString;

				// extract lifespan from author string
				Match lifespanMatch = s_lifespanRegex.Match(worksheet.Author);
				if (lifespanMatch.Success)
				{
					authorString = lifespanMatch.Groups[1].Value.Trim();

					// maybe *now* it's a creator;
					if (CreatorUtility.TryGetCreatorTemplate(authorString, out creator))
					{
						replaceType = CreatorReplaceType.RemoveLifespan;
					}
					else
					{
						// search for a creator by name/DOB/DOD
						//TODO: cache result
						string dob = lifespanMatch.Groups[2].Value.Trim();
						string dod = lifespanMatch.Groups[3].Value.Trim();
						Entity wikidata = CommonsCreatorFromWikidata.GetWikidata(authorString, dob, dod);
						if (!Entity.IsNullOrMissing(wikidata))
						{
							if (wikidata.TryGetClaimValueAsString(Wikidata.Prop_CommonsCreator, out string[] creators))
							{
								//TODO: check exists
								creator = new PageTitle("Creator", creators[0]);
							}
							else
							{
								CommonsCreatorFromWikidata.TryMakeCreator(wikidata, out creator);
							}
						}
					}
				}
				else
				{
					authorString = worksheet.Author;
				}

				// extract name from redlinked creator
				if (CreatorUtility.TryGetCreatorTemplate(authorString, out PageTitle creatorTemplate))
				{
					//OPT: multiple checks against existence of this page
					if (Article.IsNullOrMissing(GlobalAPIs.Commons.GetPage(creatorTemplate)))
					{
						authorString = creatorTemplate.Name;
					}
				}

				// unwrap author iwlink
				if (creator.IsEmpty)
				{
					Match interwikiLinkMatch = s_interwikiLinkRegex.Match(authorString);
					if (interwikiLinkMatch.Success)
					{
						authorString = interwikiLinkMatch.Groups[2].Value.Trim();

						Article interwikiArticle = GetInterwikiPage(interwikiLinkMatch.Groups[1].Value, interwikiLinkMatch.Groups[2].Value);
						if (!Article.IsNullOrMissing(interwikiArticle) && interwikiArticle.iwlinks != null)
						{
							string qid = null;
							PageTitle commonsPage = PageTitle.Empty;

							foreach (InterwikiLink iwlink in interwikiArticle.iwlinks)
							{
								if (iwlink.prefix == "d" && iwlink.value.StartsWith("Q"))
								{
									qid = iwlink.value;
									Console.WriteLine("  Interwiki Wikidata '{0}'.", qid);

									Entity entity = GlobalAPIs.Wikidata.GetEntity(qid);
									if (!Entity.IsNullOrMissing(entity))
									{
										if (AuthorIs(authorString, entity))
										{
											if (CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle entityCreator))
											{
												creator = entityCreator;
												break;
											}
										}
										else
										{
											string entityStr = entity.labels.ContainsKey("en") ? entity.labels["en"] : entity.id;
											ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Can't match '{0}' to '{1}'.", authorString, entityStr);
										}
									}
								}
								//HACK: does not actually find wikidata link, have to go through commons
								else if (iwlink.prefix == "commons")
								{
									commonsPage = PageTitle.Parse(iwlink.value);
								}
							}

							// if no wikidata but yes Commons, try to get WD from Commons
							if (string.IsNullOrEmpty(qid) && !commonsPage.IsEmpty)
							{
								creator = GetCreatorFromCommonsPage(authorString, commonsPage);
							}
						}
					}
				}

				// unwrap author wikilink
				if (creator.IsEmpty)
				{
					Match wikiLinkMatch = s_wikiLinkRegex.Match(authorString);
					if (wikiLinkMatch.Success)
					{
						authorString = wikiLinkMatch.Groups[1].Value.Trim();
						PageTitle authorTitle = PageTitle.TryParse(authorString);
						if (!authorTitle.IsEmpty)
						{
							creator = GetCreatorFromCommonsPage(authorString, authorTitle);
						}
					}
				}

				// go looking for matching creator templates in parent cats
				if (creator.IsEmpty && SlowCategoryWalk)
				{
					creator = GetCreatorFromCategories(authorString, WikiUtils.GetCategories(worksheet.Text), 1);
				}

				// manually map
				if (creator.IsEmpty)
				{
					PageTitle articleTitle = PageTitle.Parse(article.title);
					MappingCreator mapping = m_creatorMappings.TryMapValue(authorString, articleTitle);
					if (mapping == null)
					{
						// can do nothing with that
					}
					else if (!string.IsNullOrEmpty(mapping.MappedValue))
					{
						newAuthor = mapping.MappedValue;
						mapping.FromPages.Remove(article.title);
						m_creatorMappings.SetDirty();
					}
					else if (!string.IsNullOrEmpty(mapping.MappedQID))
					{
						Entity entity = GlobalAPIs.Wikidata.GetEntity(mapping.MappedQID);
						if (!Entity.IsNullOrMissing(entity))
						{
							if (CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle entityCreator))
							{
								mapping.MappedValue = newAuthor = "{{" + entityCreator + "}}";
							}
						}
					}
				}
				else
				{
					newAuthor = "{{" + creator + "}}";
				}
			}

			// found it, place creator and update
			// do not make case-only changes
			if (!string.IsNullOrEmpty(newAuthor) && !string.Equals(newAuthor, worksheet.Author, StringComparison.InvariantCultureIgnoreCase))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Green, "  FixImplicitCreators inserting '{0}'.", newAuthor);

				string textBefore = worksheet.Text.Substring(0, worksheet.AuthorIndex);
				string textAfter = worksheet.Text.Substring(worksheet.AuthorIndex + worksheet.Author.Length);
				worksheet.Text = textBefore + newAuthor + textAfter;

				article.Changes.Add(GetEditSummary(replaceType));
				article.Dirty = true;
				return true;
			}
			else
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to replace author '{0}'.", worksheet.Author);
				return false;
			}
		}

		private static string GetEditSummary(CreatorReplaceType replaceType)
		{
			switch (replaceType)
			{
				case CreatorReplaceType.Inline:
					return "replace inline creator";
				case CreatorReplaceType.RemoveLifespan:
					return "remove redundant creator lifespan";
				case CreatorReplaceType.Implicit:
				default:
					return "replace implicit creator";
			}
		}

		private static readonly char[] s_authorTrim = new char[] { ' ', '[', ']', '.', ',', ';' };
		private static readonly Regex s_lifespanRegex = new Regex(@"^([^\(]+)\s*\(?([0-9][0-9][0-9][0-9]) ?[\-– ] ?([0-9][0-9][0-9][0-9])\)?$");
		private static readonly Regex s_interwikiLinkRegex = new Regex(@"^\[\[:?(?:w:)?([a-zA-Z]+):([^\|:]+)(?:\|(.+))?\]\]$");
		private static readonly Regex s_wikiLinkRegex = new Regex(@"^\[\[([^\|]+)(?:\|(.+))?\]\]$");

		/// <summary>
		/// Returns true if the currentAuthor string is an acceptable match against specified entity.
		/// </summary>
		private static bool AuthorIs(string currentAuthor, Entity entity)
		{
			// trim sirs and trailing "letters"?
			//TODO:

			currentAuthor = currentAuthor.Trim(s_authorTrim);

			string[] currentAuthorCommaSplit = currentAuthor.Split(',');
			string reversedCurrentAuthor = "";
			if (currentAuthorCommaSplit.Length == 2)
			{
				reversedCurrentAuthor = currentAuthorCommaSplit[1].Trim() + " " + currentAuthorCommaSplit[0].Trim();
			}

			foreach (var kv in entity.labels)
			{
				if (currentAuthor.Equals(kv.Value, StringComparison.InvariantCultureIgnoreCase)
					|| reversedCurrentAuthor.Equals(kv.Value, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			foreach (var kv in entity.aliases)
			{
				foreach (string alias in kv.Value)
				{
					if (currentAuthor.Equals(alias, StringComparison.InvariantCultureIgnoreCase)
						|| reversedCurrentAuthor.Equals(alias, StringComparison.InvariantCultureIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}

		private bool GetPageExists(PageTitle pageTitle)
		{
			if (s_ExtantPages.Contains(pageTitle))
			{
				return true;
			}
			else
			{
				bool bExists = !Article.IsNullOrMissing(GlobalAPIs.Commons.GetPage(pageTitle));
				if (bExists)
				{
					s_ExtantPages.Add(pageTitle);
				}
				return bExists;
			}
		}

		private Article GetInterwikiPage(string wiki, string page)
		{
			//TODO: cache
			switch (wiki.ToLowerInvariant())
			{
				case "w":
				case "wikipedia":
				case "en":
					return GlobalAPIs.Wikipedia("en").GetPage(page, prop: "info|iwlinks");
				case "ceb":
				case "de":
				case "fr":
				case "sv":
				case "nl":
				case "ru":
				case "es":
				case "it":
				case "pl":
				case "arz":
				case "zh":
				case "ja":
				case "uk":
				case "vi":
				case "war":
				case "ar":
				case "ptr":
				case "fa":
				case "ca":
				case "id":
				case "sr":
				case "ko":
					return GlobalAPIs.Wikipedia(wiki).GetPage(page, prop: "info|iwlinks");
				default:
					//TODO: implement more wikis
					return null;
			}
		}

		public static string GetEntityIdForCreator(PageTitle creator)
		{
			Article article = GlobalAPIs.Commons.GetPage(creator.ToString());
			if (!Article.IsNullOrEmpty(article))
			{
				CommonsCreatorWorksheet worksheet = new CommonsCreatorWorksheet(article);
				return worksheet.Wikidata;
			}
			else
			{
				return null;
			}
			
		}

		public static PageTitle GetCreatorFromCommonsPage(string authorString, PageTitle pageTitle)
		{
			if (pageTitle.IsNamespace("Category"))
			{
				return GetCreatorFromCategories(authorString, new PageTitle[] { pageTitle }, 0);
			}
			else
			{
				Article commonsArticle = GlobalAPIs.Commons.GetPage(pageTitle);
				if (!Article.IsNullOrMissing(commonsArticle) && commonsArticle.revisions != null)
				{
					return GetCreatorFromCategories(authorString, WikiUtils.GetCategories(commonsArticle), 1);
				}
				else
				{
					return PageTitle.Empty;
				}
			}
		}

		/// <summary>
		/// Searches a list of categories and their parents for a creator template matching the <paramref name="authorString"/>.
		/// </summary>
		public static PageTitle GetCreatorFromCategories(string authorString, IEnumerable<PageTitle> categories, int remainingDepth)
		{
			HashSet<string> outNewEntities = new HashSet<string>();

			// go searching through cats that haven't been visited yet
			CacheCategoryEntities(categories, remainingDepth, outNewEntities);

			outNewEntities.Remove("Q000");

			// fetch any new entities
			foreach (Entity newEntity in GlobalAPIs.Wikidata.GetEntities(outNewEntities.ToList()))
			{
				// only cache people
				if (newEntity.HasClaim(Wikidata.Prop_InstanceOf) && newEntity.GetClaimValueAsEntityId(Wikidata.Prop_InstanceOf) == Wikidata.Entity_Human)
				{
					s_Entities[newEntity.id] = newEntity;

					// trim out unneeded data to save memory
					newEntity.descriptions = null;
					newEntity.sitelinks = null;
					newEntity.raw = null;
					string[] claimKeys = newEntity.claims.Keys.ToArray();
					foreach (string key in claimKeys)
					{
						if (key != Wikidata.Prop_InstanceOf && key != Wikidata.Prop_CommonsCreator)
						{
							newEntity.claims.Remove(key);
						}
					}
				}
			}

			// check over all relevant entities
			foreach (PageTitle category in categories)
			{
				if (s_CategoriesToEntities.TryGetValue(category, out List<string> entityIds))
				{
					foreach (string entityId in entityIds)
					{
						if (s_Entities.TryGetValue(entityId, out Entity entity))
						{
							if (authorString == null || AuthorIs(authorString, entity))
							{
								if (CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle entityCreator))
								{
									return entityCreator;
								}
							}
							else
							{
								string entityStr = entity.labels.ContainsKey("en") ? entity.labels["en"] : entity.id;
								ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Can't match '{0}' to '{1}'.", authorString, entityStr);
							}
						}
					}
				}
			}

			return PageTitle.Empty;
		}

		/// <summary>
		/// Caches all <see cref="Entity"/>s related to the specified categories or their parents.
		/// </summary>
		private static void CacheCategoryEntities(IEnumerable<PageTitle> categories, int remainingDepth, HashSet<string> outNewEntities)
		{
			foreach (Article category in GlobalAPIs.Commons.GetPages(
				categories
					.Where((cat) => !s_VisitedCats.Contains(cat))
					.Select((cat) => cat.ToString())
					.ToList(),
				prop: "info|revisions|iwlinks", iwprefix: "d"))
			{
				CacheCategoryEntities(category, remainingDepth, outNewEntities);
			}
		}

		/// <summary>
		/// Caches all Entities related to the specified category or its parents.
		/// </summary>
		private static void CacheCategoryEntities(Article category, int remainingDepth, HashSet<string> outNewEntities)
		{
			if (!Article.IsNullOrEmpty(category))
			{
				Console.WriteLine("  Category '{0}'.", category.title);
				PageTitle categoryTitle = PageTitle.Parse(category.title);
				string categoryText = category.revisions[0].text;

				s_VisitedCats.Add(categoryTitle);

				// embedded creator
				string creatorTemplate = WikiUtils.TrimTemplate(WikiUtils.ExtractTemplate(categoryText, "Creator"));
				if (!string.IsNullOrEmpty(creatorTemplate))
				{
					PageTitle creator = PageTitle.TryParse(creatorTemplate);
					if (creator.IsNamespace("Creator"))
					{
						string qid = GetEntityIdForCreator(creator);
						if (!string.IsNullOrEmpty(qid))
						{
							qid = Qidify(qid);
							Console.WriteLine("  Creator Wikidata '{0}'.", qid);
							AddCategoryEntity(categoryTitle, qid);
							outNewEntities.Add(qid);
						}
					}
					else
					{
						string qid = WikiUtils.GetTemplateParameter("wikidata", creatorTemplate);
						if (!string.IsNullOrEmpty(qid))
						{
							qid = Qidify(qid);
							Console.WriteLine("  Creator Wikidata '{0}'.", qid);
							AddCategoryEntity(categoryTitle, qid);
							outNewEntities.Add(qid);
						}
					}
				}

				// look up in wikidata
				if (category.iwlinks != null)
				{
					foreach (InterwikiLink iwlink in category.iwlinks)
					{
						if (s_qidRegex.IsMatch(iwlink.value))
						{
							string qid = iwlink.value;
							Console.WriteLine("  Interwiki Wikidata '{0}'.", qid);
							AddCategoryEntity(categoryTitle, qid);
							outNewEntities.Add(qid);
						}
					}
				}

				// "Wikidata Infobox" override ID
				string infoboxTemplate = WikiUtils.ExtractTemplate(categoryText, "Wikidata Infobox"); //TODO: redirect template names
				if (!string.IsNullOrEmpty(infoboxTemplate))
				{
					string qid = WikiUtils.GetTemplateParameter("qid", infoboxTemplate);
					if (!string.IsNullOrEmpty(qid))
					{
						qid = Qidify(qid);
						Console.WriteLine("  Explicit Wikidata '{0}'.", qid);
						AddCategoryEntity(categoryTitle, qid);
						outNewEntities.Add(qid);
					}
				}

				// check parent cats
				if (remainingDepth > 0 ||
					(remainingDepth > -2 && category.GetTitle().Contains(" by ")))
				{
					HashSet<string> newEntities = new HashSet<string>();
					CacheCategoryEntities(WikiUtils.GetCategories(categoryText), remainingDepth - 1, newEntities);

					// all child entities are also credited to me
					foreach (string qid in newEntities)
					{
						AddCategoryEntity(categoryTitle, qid);
					}

					outNewEntities.AddRange(newEntities);
				}
			}
		}

		private static Regex s_qidRegex = new Regex("^Q[0-9]+$");

		private static string Qidify(string qid)
		{
			if (qid.StartsWith("Q"))
			{
				return qid;
			}
			else
			{
				return "Q" + qid;
			}
		}

		private static void AddCategoryEntity(PageTitle category, string qid)
		{
			if (s_CategoriesToEntities.TryGetValue(category, out List<string> qids))
			{
				qids.AddUnique(qid);
			}
			else
			{
				s_CategoriesToEntities.Add(category, new List<string> { qid });
			}
		}
	}
}
