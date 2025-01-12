using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
				|| string.Equals(author, "non identifié", StringComparison.InvariantCultureIgnoreCase);
		}

		private static bool IsConvertibleAnonymousAuthor(string author)
		{
			author = author.Trim(' ', ';', '.', ',');
			return string.Equals(author, "anonymous", StringComparison.InvariantCultureIgnoreCase);
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
			Console.WriteLine("FixImplictCreators: checking '{0}'...", article.title);

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
				newAuthor = "{{unknown|author}}";
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
				// go looking for matching creator templates in parent cats
				PageTitle creator = GetCreatorForCategories(WikiUtils.GetCategories(worksheet.Text), 1);
				if (!creator.IsEmpty)
				{
					//TODO: better determination of if these two strings match
					if (worksheet.Author.IndexOf(creator.Name, StringComparison.InvariantCultureIgnoreCase) >= 0)
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
					else
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("  Non-matching creator template '{0}'.", creator);
						Console.ResetColor();
					}
				}
			}

			// found it, place creator and update
			// do not make case-only changes
			if (!string.IsNullOrEmpty(newAuthor) && !string.Equals(newAuthor, worksheet.Author, StringComparison.InvariantCultureIgnoreCase))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("  FixImplicitCreators inserting creator '{0}'.", newAuthor);
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

		/// <summary>
		/// Searches a list of categories and its parents for a creator template.
		/// </summary>
		private static PageTitle GetCreatorForCategories(IEnumerable<PageTitle> categories, int currentDepth)
		{
			// check cache
			foreach (PageTitle category in categories)
			{
				if (s_CategoriesToCreators.TryGetValue(category, out PageTitle creator))
				{
					return creator;
				}
			}

			// go searching
			foreach (Article category in GlobalAPIs.Commons.GetPages(categories.Select((cat) => cat.ToString()).ToList(), prop: "info|revisions|iwlinks", iwprefix: "d"))
			{
				Console.WriteLine("  Category '{0}'.", category.title);

				PageTitle parentCreator = GetCreatorForCategory(category, currentDepth);
				if (!parentCreator.IsEmpty)
				{
					return parentCreator;
				}
			}

			return PageTitle.Empty;
		}

		/// <summary>
		/// Searches the specified category for a creator template.
		/// </summary>
		private static PageTitle GetCreatorForCategory(Article category, int currentDepth)
		{
			if (!Article.IsNullOrEmpty(category))
			{
				string categoryText = category.revisions[0].text;

				// embedded creator
				string creatorTemplate = WikiUtils.ExtractTemplate(categoryText, "Creator");
				if (!string.IsNullOrEmpty(creatorTemplate))
				{
					PageTitle creator = PageTitle.Parse(creatorTemplate);
					s_CategoriesToCreators[PageTitle.Parse(category.title)] = creator;
					return creator;
				}

				// look up in wikidata
				if (category.iwlinks != null)
				{
					foreach (InterwikiLink iwlink in category.iwlinks)
					{
						if (iwlink.prefix == "d")
						{
							string entityId = iwlink.value;
							Console.WriteLine("  Interwiki Wikidata '{0}'.", entityId);
							Entity entity = GlobalAPIs.Wikidata.GetEntity(entityId);
							if (entity.TryGetClaimValueAsString(Wikidata.Prop_CommonsCreator, out string[] wikidataCreator))
							{
								//TODO: check multiple values
								return new PageTitle("Creator", wikidataCreator[0]);
							}
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
						Entity entity = GlobalAPIs.Wikidata.GetEntity(qid);
						if (entity.TryGetClaimValueAsString(Wikidata.Prop_CommonsCreator, out string[] wikidataCreator))
						{
							//TODO: check multiple values
							return new PageTitle("Creator", wikidataCreator[0]);
						}
					}
				}

				// check parent cats
				if (currentDepth < s_SearchDepth ||
					(currentDepth < s_SearchDepth + 1 && category.GetTitle().Contains(" by ")))
				{
					PageTitle parentCreator = GetCreatorForCategories(WikiUtils.GetCategories(categoryText), currentDepth + 1);
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
