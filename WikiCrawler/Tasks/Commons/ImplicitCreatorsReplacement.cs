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
				|| string.Equals(author, "anonimus", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsUnknownAuthor(string author)
		{
			return IsConvertibleUnknownAuthor(author)
				|| string.Equals(author, "{{unknown|author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{unknown author}}", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "{{author|unknown}}", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsAnonymousAuthor(string author)
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
			}
			else if (CreatorUtility.CreatorTemplateRegex.IsMatch(worksheet.Author))
			{
				// already a creator - do nothing
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

				// search for a creator by name/DOB/DOD
				Match lifespanMatch = s_lifespanRegex.Match(worksheet.Author);
				if (lifespanMatch.Success)
				{
					authorString = lifespanMatch.Groups[1].Value.Trim();
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
				else
				{
					authorString = worksheet.Author;
				}

				// unwrap author wikilink
				if (creator.IsEmpty)
				{
					Match wikilinkMatch = s_wikilinkRegex.Match(authorString);
					if (wikilinkMatch.Success)
					{
						authorString = wikilinkMatch.Groups[2].Value.Trim();

						Article interwikiArticle = GetInterwikiPage(wikilinkMatch.Groups[1].Value, wikilinkMatch.Groups[2].Value);
						if (!Article.IsNullOrMissing(interwikiArticle) && interwikiArticle.iwlinks != null)
						{
							string qid = null;
							string commonsPage = null;

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
									commonsPage = iwlink.value;
								}
							}

							// if no wikidata but yes Commons, try to get WD from Commons
							if (string.IsNullOrEmpty(qid) && !string.IsNullOrEmpty(commonsPage))
							{
								creator = GetCreatorFromCategories(authorString, new PageTitle[] { PageTitle.Parse(commonsPage) }, int.MaxValue);
							}
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

					// assign the creator template to the cache
					Creator cached = CreatorUtility.GetCreator(newAuthor, out bool bIsNew);
					if (bIsNew || !string.IsNullOrEmpty(cached.Author))
					{
						cached.Author = newAuthor;
					}

					// look up death year as well
					if (bIsNew || cached.DeathYear == 9999)
					{
						cached.DeathYear = PdOldAuto.GetCreatorDeathYear(creator);
					}

					// redirect the author string
					CreatorUtility.AddRedirect(worksheet.Author, newAuthor);
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
		private static readonly Regex s_lifespanRegex = new Regex(@"^(.+)\s*\(?([0-9][0-9][0-9][0-9]) ?[\-– ] ?([0-9][0-9][0-9][0-9])\)?$");
		private static readonly Regex s_wikilinkRegex = new Regex(@"^\[\[w?:?([a-z]+):(.+)\|(.+)\]\]$"); //TODO: support no label supplied

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
			switch (wiki)
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

		/// <summary>
		/// Searches a list of categories and their parents for a creator template matching the <paramref name="authorString"/>.
		/// </summary>
		private static PageTitle GetCreatorFromCategories(string authorString, IEnumerable<PageTitle> categories, int currentDepth)
		{
			HashSet<string> outNewEntities = new HashSet<string>();

			// go searching through cats that haven't been visited yet
			CacheCategoryEntities(categories, currentDepth, outNewEntities);

			// fetch any new entities
			foreach (Entity newEntity in GlobalAPIs.Wikidata.GetEntities(outNewEntities.ToList()))
			{
				if (newEntity.HasClaim(Wikidata.Prop_CommonsCreator))
				{
					s_Entities[newEntity.id] = newEntity;
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
