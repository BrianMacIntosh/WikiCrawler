using MediaWiki;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
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
		public enum ReplacementStatus
		{
			NotReplaced = 0,
			Replaced = 1,
		}

		public override bool UseHeartbeat
		{
			get { return true; }
		}

		/// <summary>
		/// If set, skips files that are already cached.
		/// </summary>
		public static bool SkipCached = true;

		/// <summary>
		/// If set, will walk up categories to try to map creator string.
		/// </summary>
		public static bool SlowCategoryWalk = true;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public readonly string ProjectDataDirectory;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string StaticDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "ImplicitCreatorsReplacement"); }
		}

		/// <summary>
		/// Database caching information about files that have been examined so far.
		/// </summary>
		private SQLiteConnection m_filesDatabase;

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

		private static ManualMapping<MappingCreator> s_creatorMappings;

		public static string CreatorMappingFile
		{
			get { return GetCreatorMappingFile(StaticDataDirectory); }
		}

		public static string GetCreatorMappingFile(string projectDataDirectory)
		{
			return Path.Combine(projectDataDirectory, "creator-mappings.txt");
		}

		public static string FilesDatabaseFile
		{
			get { return Path.Combine(StaticDataDirectory, "implicitcreators.db"); }
		}

		public static bool IsUnknownOrAnonymousAuthor(string author)
		{
			return IsUnknownAuthor(author) || IsAnonymousAuthor(author);
		}

		private static bool IsConvertibleUnknownAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			if (IsConvertibleSingularUnknownAuthor(author))
			{
				return true;
			}

			// check for multiple language templates
			bool hasLanguageMatch = false;
			foreach (string innerAuthor in StripLanguageTemplates(author))
			{
				if (IsConvertibleSingularUnknownAuthor(innerAuthor))
				{
					hasLanguageMatch = true;
				}
				else
				{
					return false;
				}
			}

			return hasLanguageMatch;
		}

		private static bool IsConvertibleSingularUnknownAuthor(string author)
		{
			return string.Equals(author, "unknown", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocido", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "desconocida", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "sconosciuto", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "unbekannt", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "неизвестен", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
				|| string.Equals(author, "不明", StringComparison.InvariantCultureIgnoreCase) //TODO: rerun
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
			if (IsConvertibleSingularAnonymousAuthor(author))
			{
				return true;
			}

			// check for multiple language templates
			bool hasLanguageMatch = false;
			foreach (string innerAuthor in StripLanguageTemplates(author))
			{
				if (IsConvertibleSingularAnonymousAuthor(innerAuthor))
				{
					hasLanguageMatch = true;
				}
				else
				{
					return false;
				}
			}

			return hasLanguageMatch;
		}

		private static bool IsConvertibleSingularAnonymousAuthor(string author)
		{
			return string.Equals(author, "anonymous", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymouse", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymous artist", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymos artist", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonimo", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonym", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anon", StringComparison.InvariantCultureIgnoreCase)
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
				|| (onlyTemplateName != null && onlyTemplateName.StartsWith("Creator:Anonymous", StringComparison.InvariantCultureIgnoreCase))
				|| string.Equals(onlyTemplateName, "Creator:Anon", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anonimo", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anonyme", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anonym", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anoniem", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(onlyTemplateName, "Creator:Anònim", StringComparison.InvariantCultureIgnoreCase)
				|| string.Equals(author, "anonymous plate", StringComparison.InvariantCultureIgnoreCase); // don't know what to replace this with
		}

		public enum CreatorReplaceType
		{
			Implicit,
			Inline,
			Mapped,
			RemoveLifespan,

			/// <summary>
			/// No actual change made.
			/// </summary>
			Identity,

			None
		}

		static ImplicitCreatorsReplacement()
		{
			s_creatorMappings = new ManualMapping<MappingCreator>(CreatorMappingFile);
		}

		public ImplicitCreatorsReplacement(string directory)
		{
			ProjectDataDirectory = Path.Combine(Configuration.DataDirectory, directory);
			Directory.CreateDirectory(ProjectDataDirectory);
			m_filesDatabase = ConnectFilesDatabase(true);
		}

		public static SQLiteConnection ConnectFilesDatabase(bool bWantsWrite)
		{
			SQLiteConnectionStringBuilder connectionString = new SQLiteConnectionStringBuilder
			{
				{ "Data Source", FilesDatabaseFile },
				{ "Mode", bWantsWrite ? "ReadWrite" : "ReadOnly" }
			};
			SQLiteConnection connection = new SQLiteConnection(connectionString.ConnectionString);
			connection.Open();
			return connection;
		}

		public override void SaveOut()
		{
			base.SaveOut();

			s_creatorMappings.Serialize();
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
			PageTitle articleTitle = PageTitle.Parse(article.title);

			if (SkipCached && IsFileCached(m_filesDatabase, articleTitle))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Already cached.");
				return false;
			}

			CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article);

			if (string.IsNullOrEmpty(worksheet.Author))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to find author.");
				return false;
			}

			Console.WriteLine("  Author string is '{0}'.", worksheet.Author);

			CacheFile(articleTitle, worksheet.Author);

			CreatorReplaceType replaceType;

			// map the author string to a template
			string newAuthor;
			if (suggestedCreator != null)
			{
				replaceType = CreatorReplaceType.Mapped;

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
			else
			{
				newAuthor = MapAuthorTemplate(worksheet, out replaceType);
			}

			if (replaceType == CreatorReplaceType.Identity)
			{
				ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "  Author '{0}' is already satisfactory.", worksheet.Author);
				return false;
			}
			else if (string.IsNullOrEmpty(newAuthor))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "  Failed to replace author '{0}'.", worksheet.Author);
				return false;
			}
			// found it, place creator and update
			// do not make case-only changes
			else if (!string.Equals(newAuthor, worksheet.Author, StringComparison.InvariantCultureIgnoreCase))
			{
				CacheReplacementStatus(articleTitle, ReplacementStatus.Replaced);

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
				// no change desired
				return false;
			}
		}

		/// <summary>
		/// If the specified string can be definitively mapped to a template (e.g. creator), returns the template.
		/// </summary>
		public static string MapAuthorTemplate(CommonsFileWorksheet worksheet)
		{
			return MapAuthorTemplate(worksheet, out CreatorReplaceType replaceType);
		}

		/// <summary>
		/// If the specified string can be definitively mapped to a template (e.g. creator), returns the template.
		/// </summary>
		public static string MapAuthorTemplate(CommonsFileWorksheet worksheet, out CreatorReplaceType replaceType)
		{
			//TODO: if everything is in a language tag and every language tag comes up with the same result, replace

			replaceType = CreatorReplaceType.Implicit;
			string authorString = worksheet.Author;

			if (IsConvertibleUnknownAuthor(authorString))
			{
				if (string.Equals(worksheet.AuthorParam, "artist") || string.Equals(worksheet.AuthorParam, "artist_display_name"))
				{
					return "{{unknown|artist}}";
				}
				else if (string.Equals(worksheet.AuthorParam, "photographer"))
				{
					return "{{unknown photographer}}";
				}
				else
				{
					return "{{unknown|author}}";
				}
			}
			else if (IsConvertibleAnonymousAuthor(authorString))
			{
				return "{{anonymous}}";
			}
			else if (IsConvertibleUnknownArtist(authorString))
			{
				return "{{unknown|artist}}";
			}
			else if (IsUnknownAuthor(authorString) || IsAnonymousAuthor(authorString))
			{
				// already an anon/unknown template - do nothing
				ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "  Already a template");
				return "";
			}

			if (TryMapMultiTemplate(worksheet, authorString, out CreatorTemplate mappedMultiLanguage, out replaceType))
			{
				return mappedMultiLanguage.ToString();
			}
			else
			{
				return "";
			}
		}

		/// <summary>
		/// Tries to map an author string that is already known to not be a template to a creator template.
		/// </summary>
		private static bool TryMapCreatorTemplate(CommonsFileWorksheet worksheet, string authorString, out CreatorTemplate creator, out CreatorReplaceType replaceType)
		{
			creator = AutoMapNonTemplateAuthor(worksheet, authorString, out replaceType);
			return !creator.IsEmpty;
		}

		/// <summary>
		/// Tries to map a raw author string to a creator template.
		/// </summary>
		private static CreatorTemplate AutoMapNonTemplateAuthor(CommonsFileWorksheet worksheet, string authorString, out CreatorReplaceType replaceType)
		{
			ConsoleUtility.WriteLine(ConsoleColor.Gray, "  Component '{0}'", authorString);

			replaceType = CreatorReplaceType.Implicit;

			if (IsUnknownOrAnonymousAuthor(authorString))
			{
				//TODO: support multi-component unknown/anon
				ConsoleUtility.WriteLine(ConsoleColor.Red, "    Unknown/anon");
				return new CreatorTemplate();
			}

			// extract lifespan from author string
			Match lifespanMatch = s_lifespanRegex.Match(authorString);
			if (lifespanMatch.Success)
			{
				authorString = lifespanMatch.Groups[1].Value.Trim();
			}

			if (CreatorUtility.TryGetCreatorTemplate(authorString, out CreatorTemplate creatorTemplate))
			{
				// already a creator
				if (GetPageExists(creatorTemplate.Template))
				{
					ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "    Already a creator");

					// creator exists
					if (lifespanMatch.Success)
					{
						// only stripping lifespan
						replaceType = CreatorReplaceType.RemoveLifespan;
						return creatorTemplate;
					}
					else
					{
						// do nothing

						//HACK: make sure this file is not still in the creator mappings
						foreach (var kv in s_creatorMappings)
						{
							kv.Value.FromPages.Remove(worksheet.Article.title);
							s_creatorMappings.SetDirty();
						}

						replaceType = CreatorReplaceType.Identity;
						return creatorTemplate;
					}
				}
				else
				{
					// redlinked creator
					ConsoleUtility.WriteLine(ConsoleColor.Yellow, "    Author is redlink creator '{0}'", creatorTemplate);

					// extract name from redlinked creator
					//OPT: multiple checks against existence of this page
					if (Article.IsNullOrMissing(GlobalAPIs.Commons.GetPage(creatorTemplate.Template)))
					{
						authorString = creatorTemplate.Template.Name;
					}

					if (TryMapCreatorTemplate(worksheet, authorString, out CreatorTemplate mappedRedlinkAuthor, out replaceType))
					{
						if (string.IsNullOrWhiteSpace(mappedRedlinkAuthor.Option)
							&& string.IsNullOrWhiteSpace(creatorTemplate.Option))
						{
							return mappedRedlinkAuthor.Template;
						}
						else
						{
							//TODO: support and test
						}
					}
				}
			}
			else if (CreatorUtility.InlineCreatorTemplateRegex.MatchOut(authorString, out Match inlineCreatorMatch))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    Inline Template");

				// inline-QID creator
				Entity entity = GlobalAPIs.Wikidata.GetEntity(inlineCreatorMatch.Groups[1].Value); //TODO: use cache
				if (!Entity.IsNullOrMissing(entity))
				{
					PageTitle creator;
					CommonsCreatorFromWikidata.TryMakeCreator(entity, out creator);
					replaceType = CreatorReplaceType.Inline;
					return creator;
				}
				else
				{
					// QID is invalid
					//TODO:?
					ConsoleUtility.WriteLine(ConsoleColor.Red, "    Invalid QID");
					return new CreatorTemplate();
				}
			}

			string authorTemplate = WikiUtils.GetOnlyTemplateName(authorString);
			if (string.IsNullOrEmpty(authorTemplate))
			{
				// not a template
			}
			else if (authorTemplate.Equals("c", StringComparison.OrdinalIgnoreCase))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    'c' Template");

				string template = WikiUtils.ExtractTemplate(authorString, authorTemplate);
				string category = WikiUtils.GetTemplateParameter(1, template);
				PageTitle categoryPage = PageTitle.TryParse(category);
				if (categoryPage.IsEmpty)
				{
					categoryPage = new PageTitle("Category", category);
				}

				PageTitle creator = GetCreatorFromCategories(null, new PageTitle[] { categoryPage }, 0);
				if (!creator.IsEmpty)
				{
					replaceType = CreatorReplaceType.Identity;
					return creator;
				}
			}
			else if (authorTemplate.Equals("q", StringComparison.OrdinalIgnoreCase))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    Q template");

				string template = WikiUtils.ExtractTemplate(authorString, authorTemplate);
				string id = WikiUtils.GetTemplateParameter(1, template);
				if (Wikidata.TryUnQidify(id, out int authorQid)
					|| int.TryParse(id, out authorQid))
				{
					Entity entity = GlobalAPIs.Wikidata.GetEntity("Q" + authorQid);
					if (!Entity.IsNullOrMissing(entity) && CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle qidCreator))
					{
						replaceType = CreatorReplaceType.Identity;
						return qidCreator;
					}
				}
			}
			else if (authorTemplate.StartsWith("#property", StringComparison.OrdinalIgnoreCase))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    #property invocation");

				//TODO:
			}

			// literal qid
			if (Wikidata.TryUnQidify(authorString, out int literalQid))
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    Literal QID");

				Entity entity = GlobalAPIs.Wikidata.GetEntity("Q" + literalQid);
				if (!Entity.IsNullOrMissing(entity) && CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle literalQidCreator))
				{
					replaceType = CreatorReplaceType.Identity;
					return literalQidCreator;
				}
			}

			if (lifespanMatch.Success)
			{
				// search for a creator by name/DOB/DOD
				//TODO: cache result
				string dob = lifespanMatch.Groups[2].Value.Trim();
				string dod = lifespanMatch.Groups[3].Value.Trim();
				Entity wikidata = CommonsCreatorFromWikidata.GetWikidata(authorString, dob, dod);
				if (!Entity.IsNullOrMissing(wikidata))
				{
					ConsoleUtility.WriteLine(ConsoleColor.DarkGreen, "    Search matched");

					if (wikidata.TryGetClaimValueAsString(Wikidata.Prop_CommonsCreator, out string[] creators))
					{
						//TODO: check exists
						return new PageTitle("Creator", creators[0]);
					}
					else if (CommonsCreatorFromWikidata.TryMakeCreator(wikidata, out PageTitle creator))
					{
						return creator;
					}
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Gray, "    Search no match");
				}
			}

			// unwrap author iwlink
			Match interwikiLinkMatch = s_interwikiLinkRegex.Match(authorString);
			if (interwikiLinkMatch.Success)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    iwlink");

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
						//HACK: does not actually find wikidata link, have to go through commons
						else if (iwlink.prefix == "commons")
						{
							commonsPage = PageTitle.Parse(iwlink.value);
						}
					}

					// if no wikidata but yes Commons, try to get WD from Commons
					if (string.IsNullOrEmpty(qid) && !commonsPage.IsEmpty)
					{
						return GetCreatorFromCommonsPage(authorString, commonsPage);
					}
				}
			}

			// unwrap author wikilink
			Match wikiLinkMatch = s_wikiLinkRegex.Match(authorString);
			if (wikiLinkMatch.Success)
			{
				ConsoleUtility.WriteLine(ConsoleColor.Gray, "    wikilink");

				authorString = wikiLinkMatch.Groups[1].Value.Trim();
				PageTitle authorTitle = PageTitle.TryParse(authorString);
				if (!authorTitle.IsEmpty)
				{
					return GetCreatorFromCommonsPage(authorString, authorTitle);
				}
			}

			// go looking for matching creator templates in parent cats
			if (SlowCategoryWalk)
			{
				PageTitle catCreator = GetCreatorFromCategories(authorString, WikiUtils.GetCategories(worksheet.Text), 1);
				if (!catCreator.IsEmpty)
				{
					return catCreator;
				}
			}

			// manually map
			MappingCreator mapping = s_creatorMappings.TryMapValue(authorString, PageTitle.Parse(worksheet.Article.title));
			if (mapping == null)
			{
				// null or empty authorString?
				return new CreatorTemplate();
			}
			else if (!string.IsNullOrEmpty(mapping.MappedValue))
			{
				if (CreatorUtility.TryGetCreatorTemplate(mapping.MappedValue, out CreatorTemplate parsedCreator))
				{
					mapping.FromPages.Remove(worksheet.Article.title);
					s_creatorMappings.SetDirty();
					return parsedCreator;
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "Can't parse mapped creator '{0}'.", mapping.MappedValue);
				}
			}
			else if (!string.IsNullOrEmpty(mapping.MappedQID))
			{
				Entity entity = GlobalAPIs.Wikidata.GetEntity(mapping.MappedQID);
				if (!Entity.IsNullOrMissing(entity))
				{
					if (CommonsCreatorFromWikidata.TryMakeCreator(entity, out PageTitle entityCreator))
					{
						mapping.MappedValue = "{{" + entityCreator + "}}";
						return entityCreator;
					}
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "Failed to find mapped creator entity with QID '{0}'.", mapping.MappedQID);
				}
			}

			return new CreatorTemplate();
		}

		/// <summary>
		/// Breaks the string down into parsed units and checks if all of them can represent one creator.
		/// </summary>
		private static bool TryMapMultiTemplate(CommonsFileWorksheet worksheet, string authorString, out CreatorTemplate creator, out CreatorReplaceType replaceType)
		{
			creator = AutoMapMultiLanguage(worksheet, authorString, out replaceType);
			return !creator.IsEmpty;
		}

		private static IEnumerable<Node> FlattenNodes(IEnumerable<Node> inNodes)
		{
			foreach (Node node in inNodes)
			{
				if (node is InlineNode inlineNode)
				{
					yield return node;
				}
				else
				{
					foreach (Node childNode in FlattenNodes(node.EnumChildren()))
					{
						yield return childNode;
					}
				}
			}
		}

		/// <summary>
		/// Reduces all language templates to their argument.
		/// </summary>
		private static IEnumerable<string> StripLanguageTemplates(string text)
		{
			WikitextParser parser = new WikitextParser();
			Wikitext wikitext = parser.Parse(text);

			foreach (Node node in FlattenNodes(wikitext.EnumChildren()))
			{
				if (node is Template templateNode)
				{
					if (CommonsUtility.IsLanguageTemplate(templateNode.Name.ToPlainText()))
					{
						yield return templateNode.Arguments[1].Value.ToString();
					}
					else
					{
						// template not a language template
						yield return templateNode.ToString();
					}
				}
				else if (node is PlainText plainTextNode)
				{
					if (!string.IsNullOrWhiteSpace(plainTextNode.Content))
					{
						yield return plainTextNode.Content;
					}
				}
				else
				{
					yield return node.ToString();
				}
			}
		}

		private static CreatorReplaceType CombineReplaceType(CreatorReplaceType a, CreatorReplaceType b)
		{
			return (CreatorReplaceType)Math.Min((int)a, (int)b);
		}

		/// <summary>
		/// Checks if the string is a series of language templates and they all match one creator.
		/// </summary>
		private static CreatorTemplate AutoMapMultiLanguage(CommonsFileWorksheet worksheet, string authorString, out CreatorReplaceType replaceType)
		{
			replaceType = CreatorReplaceType.None;

			// if all the language templates produce the same creator, the creator
			CreatorTemplate matchedCreator = new CreatorTemplate();

			foreach (string innerText in StripLanguageTemplates(authorString))
			{
				if (TryMapCreatorTemplate(worksheet, innerText, out CreatorTemplate subCreator, out CreatorReplaceType subReplaceType))
				{
					replaceType = CombineReplaceType(replaceType, subReplaceType);
					if (!string.IsNullOrWhiteSpace(subCreator.Option))
					{
						replaceType = CreatorReplaceType.None;
						ConsoleUtility.WriteLine(ConsoleColor.Red, "    Creator template with Option.");
						return new CreatorTemplate();
					}
					else if (matchedCreator.IsEmpty)
					{
						matchedCreator = subCreator;
					}
					else if (matchedCreator != subCreator)
					{
						// non-matching parsed creator
						replaceType = CreatorReplaceType.None;
						ConsoleUtility.WriteLine(ConsoleColor.Red, "    Multiple non-matching creators.");
						return new CreatorTemplate();
					}
				}
				else
				{
					// unparsed creator
					replaceType = CreatorReplaceType.None;
					ConsoleUtility.WriteLine(ConsoleColor.Red, "    Failed to parse component.");
					return new CreatorTemplate();
				}
			}

			return matchedCreator;
		}

		private static string GetEditSummary(CreatorReplaceType replaceType)
		{
			switch (replaceType)
			{
				case CreatorReplaceType.Inline:
					return "replace inline creator";
				case CreatorReplaceType.RemoveLifespan:
					return "remove redundant creator lifespan";
				case CreatorReplaceType.Mapped:
					return "replace manually mapped creator";
				case CreatorReplaceType.Implicit:
				default:
					return "replace implicit creator";
			}
		}

		public static bool IsFileCached(SQLiteConnection connection, PageTitle title)
		{
			SQLiteCommand command = connection.CreateCommand();
			command.CommandText = "SELECT COUNT(*) FROM files WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			using (var reader = command.ExecuteReader())
			{
				reader.Read();
				return reader.GetInt32(0) > 0;
			}
		}

		private void CacheFile(PageTitle title, string author)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "INSERT INTO files (pageTitle, authorString, touchTimeUnix) VALUES ($pageTitle, $authorString, unixepoch()) "
				+ "ON CONFLICT (pageTitle) DO UPDATE SET authorString=$authorString,touchTimeUnix=unixepoch()";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorString", author);
			Debug.Assert(command.ExecuteNonQuery() == 1);
		}

		private void CacheReplacementStatus(PageTitle title, ReplacementStatus state)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET replaced=$state WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("state", (int)state);
			Debug.Assert(command.ExecuteNonQuery() == 1);
		}

		//TODO:
		private void CacheAuthorInfo(PageTitle title, int? qid)
		{
			SQLiteCommand command = m_filesDatabase.CreateCommand();
			command.CommandText = "UPDATE files SET authorQid=$authorQid WHERE pageTitle=$pageTitle";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorQid", qid);
			Debug.Assert(command.ExecuteNonQuery() == 1);
		}

		private static readonly char[] s_authorTrim = new char[] { ' ', '[', ']', '.', ',', ';' };
		private static readonly Regex s_lifespanRegex = new Regex(@"^([^\(\n]+)\s*\(?([0-9][0-9][0-9][0-9]) ?[\-– ] ?([0-9][0-9][0-9][0-9])\)?$");
		private static readonly Regex s_interwikiLinkRegex = new Regex(@"^\[\[:?(?:w:)?([a-zA-Z]+):([^\|:]+)(?:\|([^\]]+))?\]\]$");
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

		private static bool GetPageExists(PageTitle pageTitle)
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

		private static Article GetInterwikiPage(string wiki, string page)
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
