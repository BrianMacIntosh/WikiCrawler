using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Replaces author strings with verified Creator templates.
	/// </summary>
	public class ImplicitCreatorsReplacement : BaseReplacement
	{
		private const int s_SearchDepth = 1;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "fiximplicitcreators"); }
		}

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

		private static bool IsConvertibleUnknownAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "unknown", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocido", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocida", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "sconosciuto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "non noto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "non identifié", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "author unknown", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsConvertibleUnknownArtist(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "Artist unknown", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsConvertibleAnonymousAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "anonymous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "auteur anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonimous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonimus", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymus", StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool IsUnknownAuthor(string author)
		{
			return IsConvertibleUnknownAuthor(author)
				|| string.Equals(author, "{{unknown|author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown|artist}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown|1=author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{author|unknown}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{creator:unknown}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{creator:?}}", StringComparison.InvariantCultureIgnoreCase);
		}

		public static bool IsAnonymousAuthor(string author)
		{
			return IsConvertibleAnonymousAuthor(author)
				|| string.Equals(author, "{{anonymous}}", StringComparison.InvariantCulture)
				|| string.Equals(author, "{{Anonymous}}", StringComparison.InvariantCulture)
				|| string.Equals(author, "{{creator:Anonymous}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{Creator:Anonymous}}", StringComparison.InvariantCultureIgnoreCase);
		}

		public override bool DoReplacement(Article article)
		{
			return DoReplacement(article, null);
		}

		/// <summary>
		/// Replace any verifiable implicit creators with creator templates.
		/// </summary>
		/// <param name="article">Article, already downloaded.</param>
		/// <param name="creator">A creator that we have already determined should be used.</param>
		/// <returns>True if a replacement was made.</returns>
		public bool DoReplacement(Article article, PageTitle? assumedCreator)
		{
			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (string.IsNullOrEmpty(worksheet.Author))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Failed to find author.");
				Console.ResetColor();
				return false;
			}

			Console.WriteLine("  Author string is '{0}'.", worksheet.Author);

			string newAuthor = "";

			// check for "anonymous" and "unknown"
			if (IsConvertibleUnknownAuthor(worksheet.Author))
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
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("  Already a template");
				Console.ResetColor();
				return false;
			}
			else if (CreatorUtility.CreatorTemplateRegex.IsMatch(worksheet.Author))
			{
				// already a creator - do nothing
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("  Already a creator");
				Console.ResetColor();
				return false;
			}
			else if (assumedCreator.HasValue && !assumedCreator.Value.IsEmpty)
			{
				// use the passed-in creator
				if (string.Equals(assumedCreator.Value.Name, worksheet.Author, StringComparison.InvariantCultureIgnoreCase))
				{
					newAuthor = "{{" + assumedCreator.Value.ToString() + "}}";
				}
			}
			else
			{
				PageTitle creator = PageTitle.Empty;
				string authorString;

				// extract lifespan from author string
				Match lifespanMatch = s_lifespanRegex.Match(worksheet.Author);
				if (lifespanMatch.Success)
				{
					authorString = lifespanMatch.Groups[1].Value.Trim();

					// maybe *now* it's a creator
					Match creatorTemplateMatch = CreatorUtility.CreatorTemplateRegex.Match(authorString);
					if (creatorTemplateMatch.Success)
					{
						creator = PageTitle.Parse(creatorTemplateMatch.Groups[1].Value);
					}
					else
					{
						// search for a creator by name/DOB/DOD
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
								if (iwlink.prefix == "d")
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
											Console.ForegroundColor = ConsoleColor.Yellow;
											Console.WriteLine("  Can't match '{0}' to '{1}'.", authorString, entityStr);
											Console.ResetColor();
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
				if (creator.IsEmpty)
				{
					creator = GetCreatorFromCategories(authorString, WikiUtils.GetCategories(worksheet.Text), 1);
				}

				if (!creator.IsEmpty)
				{
					newAuthor = "{{" + creator + "}}";
				}
			}

			// found it, place creator and update
			// do not make case-only changes
			if (!string.IsNullOrEmpty(newAuthor) && !string.Equals(newAuthor, worksheet.Author, StringComparison.InvariantCultureIgnoreCase))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("  FixImplicitCreators inserting '{0}'.", newAuthor);
				Console.ResetColor();

				string textBefore = worksheet.Text.Substring(0, worksheet.AuthorIndex);
				string textAfter = worksheet.Text.Substring(worksheet.AuthorIndex + worksheet.Author.Length);
				worksheet.Text = textBefore + newAuthor + textAfter;

				article.Changes.Add("replace implicit creator");
				article.Dirty = true;
				return true;
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  Failed to replace author '{0}'.", worksheet.Author);
				Console.ResetColor();
				return false;
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

			foreach (var kv in entity.labels)
			{
				if (currentAuthor.Equals(kv.Value, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			foreach (var kv in entity.aliases)
			{
				foreach (string alias in kv.Value)
				{
					if (currentAuthor.Equals(alias, StringComparison.InvariantCultureIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}

		private Article GetInterwikiPage(string wiki, string page)
		{
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
				string articleText = article.revisions[0].text;
				string creatorInsides = WikiUtils.ExtractTemplate(articleText, "Creator");

				if (!string.IsNullOrEmpty(creatorInsides))
				{
					string wdId = WikiUtils.GetTemplateParameter("Wikidata", creatorInsides);
					if (!string.IsNullOrEmpty(wdId))
					{
						return wdId;
					}
				}
			}

			return null;
		}

		private static PageTitle GetCreatorFromCommonsPage(string authorString, PageTitle pageTitle)
		{
			if (pageTitle.IsNamespace("Category"))
			{
				return GetCreatorFromCategories(authorString, new PageTitle[] { pageTitle }, int.MaxValue);
			}
			else
			{
				Article commonsArticle = GlobalAPIs.Commons.GetPage(pageTitle);
				return GetCreatorFromCategories(authorString, WikiUtils.GetCategories(commonsArticle), 1);
			}
		}

		/// <summary>
		/// Searches a list of categories and their parents for a creator template matching the <paramref name="authorString"/>.
		/// </summary>
		public static PageTitle GetCreatorFromCategories(string authorString, IEnumerable<PageTitle> categories, int currentDepth)
		{
			HashSet<string> outNewEntities = new HashSet<string>();

			// go searching through cats that haven't been visited yet
			CacheCategoryEntities(categories, currentDepth, outNewEntities);

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
							if (AuthorIs(authorString, entity))
							{
								if (CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle entityCreator))
								{
									return entityCreator;
								}
							}
							else
							{
								string entityStr = entity.labels.ContainsKey("en") ? entity.labels["en"] : entity.id;
								Console.ForegroundColor = ConsoleColor.Yellow;
								Console.WriteLine("  Can't match '{0}' to '{1}'.", authorString, entityStr);
								Console.ResetColor();
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
		private static void CacheCategoryEntities(IEnumerable<PageTitle> categories, int currentDepth, HashSet<string> outNewEntities)
		{
			foreach (Article category in GlobalAPIs.Commons.GetPages(
				categories
					.Where((cat) => !s_VisitedCats.Contains(cat))
					.Select((cat) => cat.ToString())
					.ToList(),
				prop: "info|revisions|iwlinks", iwprefix: "d"))
			{
				CacheCategoryEntities(category, currentDepth, outNewEntities);
			}
		}

		/// <summary>
		/// Caches all Entities related to the specified category or its parents.
		/// </summary>
		private static void CacheCategoryEntities(Article category, int currentDepth, HashSet<string> outNewEntities)
		{
			if (!Article.IsNullOrEmpty(category))
			{
				Console.WriteLine("  Category '{0}'.", category.title);
				PageTitle categoryTitle = PageTitle.Parse(category.title);
				string categoryText = category.revisions[0].text;

				s_VisitedCats.Add(categoryTitle);

				// embedded creator
				string creatorTemplate = WikiUtils.ExtractTemplate(categoryText, "Creator");
				if (!string.IsNullOrEmpty(creatorTemplate))
				{
					PageTitle creator = PageTitle.TryParse(creatorTemplate);
					if (creator.IsNamespace("Creator"))
					{
						string qid = GetEntityIdForCreator(creator);
						if (!string.IsNullOrEmpty(qid))
						{
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
						if (iwlink.prefix == "d")
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
						Console.WriteLine("  Explicit Wikidata '{0}'.", qid);
						AddCategoryEntity(categoryTitle, qid);
						outNewEntities.Add(qid);
					}
				}

				// check parent cats
				if (currentDepth < s_SearchDepth ||
					(currentDepth < s_SearchDepth + 3 && category.GetTitle().Contains(" by ")))
				{
					HashSet<string> newEntities = new HashSet<string>();
					CacheCategoryEntities(WikiUtils.GetCategories(categoryText), currentDepth + 1, newEntities);

					// all child entities are also credited to me
					foreach (string qid in newEntities)
					{
						AddCategoryEntity(categoryTitle, qid);
					}

					outNewEntities.AddRange(newEntities);
				}
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
