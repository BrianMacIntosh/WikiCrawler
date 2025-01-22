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

		private static Dictionary<PageTitle, PageTitle> s_CategoriesToCreators = new Dictionary<PageTitle, PageTitle>();

		private static bool IsConvertibleUnknownAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "unknown", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocido", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocida", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "sconosciuto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "non noto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "non identifié", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsConvertibleAnonymousAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "anonymous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "auteur anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonimous", StringComparison.InvariantCultureIgnoreCase);
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
				Console.WriteLine("  Failed to find author string.");
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

				// unwrap wikilink
				if (creator.IsEmpty)
				{
					Match wikilinkMatch = s_wikilinkRegex.Match(authorString);
					if (wikilinkMatch.Success)
					{
						//TODO: could use wikilink for looking up, or check both sides
						authorString = wikilinkMatch.Groups[1].Value;
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
				return false;
			}
		}

		private static readonly char[] s_authorTrim = new char[] { ' ', '[', ']', '.', ',', ';' };
		private static readonly Regex s_lifespanRegex = new Regex(@"^(.+) ?\(([0-9]+)[\-– ]([0-9]+)\)$");
		private static readonly Regex s_wikilinkRegex = new Regex(@"^\[\[:?([a-z]+):(.+)\|(.+)\]\]$"); //TODO: support no label supplied

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

		private struct CheckEntity
		{
			/// <summary>
			/// The page where the entity was found.
			/// </summary>
			public PageTitle SourcePage;

			public string EntityId;

			public CheckEntity(PageTitle inSourcePage, string inEntityId)
			{
				SourcePage = inSourcePage;
				EntityId = inEntityId;
			}
		}

		/// <summary>
		/// Searches a list of categories and its parents for a creator template.
		/// </summary>
		private static PageTitle GetCreatorFromCategories(string authorString, IEnumerable<PageTitle> categories, int currentDepth)
		{
			// check cache
			foreach (PageTitle category in categories)
			{
				if (s_CategoriesToCreators.TryGetValue(category, out PageTitle creator))
				{
					return creator;
				}
			}

			// list of Wikidata entity IDs to check
			List<CheckEntity> entityIds = new List<CheckEntity>();

			// go searching through cats
			foreach (Article category in GlobalAPIs.Commons.GetPages(categories.Select((cat) => cat.ToString()).ToList(), prop: "info|revisions|iwlinks", iwprefix: "d"))
			{
				Console.WriteLine("  Category '{0}'.", category.title);

				PageTitle parentCreator = GetCreatorForCategory(authorString, category, currentDepth, entityIds);
				if (!parentCreator.IsEmpty)
				{
					s_CategoriesToCreators[PageTitle.Parse(category.title)] = parentCreator;
					return parentCreator;
				}
			}

			// check any collected Wikidata entities
			foreach (Entity entity in GlobalAPIs.Wikidata.GetEntities(entityIds.Select(ent => ent.EntityId).Distinct().ToArray()))
			{
				if (AuthorIs(authorString, entity))
				{
					if (CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle creator))
					{
						foreach (CheckEntity ent in entityIds)
						{
							if (ent.EntityId == entity.id)
							{
								s_CategoriesToCreators[ent.SourcePage] = creator;
							}
						}
						
						return creator;
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

			return PageTitle.Empty;
		}

		public static Entity GetEntityForCreator(PageTitle creator)
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
						return GlobalAPIs.Wikidata.GetEntity(wdId);
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Searches the specified category for a creator template.
		/// </summary>
		/// <param name="outEntityIdsBuffer">Any entities that should be checked for creators are added to this list.</param>
		private static PageTitle GetCreatorForCategory(string authorString, Article category, int currentDepth, List<CheckEntity> outEntityIdsBuffer)
		{
			if (!Article.IsNullOrEmpty(category))
			{
				string categoryText = category.revisions[0].text;

				// embedded creator
				string creatorTemplate = WikiUtils.ExtractTemplate(categoryText, "Creator");
				if (!string.IsNullOrEmpty(creatorTemplate))
				{
					PageTitle creator = PageTitle.TryParse(creatorTemplate);
					if (creator.IsNamespace("Creator"))
					{
						Entity creatorEntity = GetEntityForCreator(creator);
						if (!Entity.IsNullOrMissing(creatorEntity) && AuthorIs(authorString, creatorEntity))
						{
							s_CategoriesToCreators[PageTitle.Parse(category.title)] = creator;
							return creator;
						}
					}
					else
					{
						string wikidataId = WikiUtils.GetTemplateParameter("wikidata", creatorTemplate);
						if (!string.IsNullOrEmpty(wikidataId))
						{
							Console.WriteLine("  Creator Wikidata '{0}'.", wikidataId);
							Entity entity = GlobalAPIs.Wikidata.GetEntity(wikidataId);
							if (entity != null
								&& AuthorIs(authorString, entity)
								&& entity.TryGetClaimValueAsString(Wikidata.Prop_CommonsCreator, out string[] wikidataCreator))
							{
								//TODO: check multiple values
								creator = new PageTitle("Creator", wikidataCreator[0]);
								s_CategoriesToCreators[PageTitle.Parse(category.title)] = creator;
								return creator;
							}
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
							outEntityIdsBuffer.Add(new CheckEntity(PageTitle.Parse(category.title), qid));
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
						outEntityIdsBuffer.Add(new CheckEntity(PageTitle.Parse(category.title), qid));
					}
				}

				// check parent cats
				if (currentDepth < s_SearchDepth ||
					(currentDepth < s_SearchDepth + 1 && category.GetTitle().Contains(" by ")))
				{
					PageTitle parentCreator = GetCreatorFromCategories(authorString, WikiUtils.GetCategories(categoryText), currentDepth + 1);
					if (!parentCreator.IsEmpty)
					{
						s_CategoriesToCreators[PageTitle.Parse(category.title)] = parentCreator;
						return parentCreator;
					}
				}
			}

			return PageTitle.Empty;
		}
	}
}
