using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WikiCrawler
{
	public class MappingCategory : MappingValue
	{
		public string MappedCategory;

		public override bool IsUnmapped => string.IsNullOrEmpty(MappedCategory);
	}

	/// <summary>
	/// Helper functions for translating raw content into categories.
	/// </summary>
	public class CategoryMapping : ManualMapping<MappingCategory>
	{
		//private static MySqlConnection taxonConnection;

		/// <summary>
		/// Cached tree structure of Commons categories.
		/// </summary>
		public static readonly CategoryTree CommonsCategoryTree = new CategoryTree(GlobalAPIs.Commons);

		private static Dictionary<PageTitle, Article> s_extantCategories = new Dictionary<PageTitle, Article>();

		private static char[] s_trim = new char[] { ' ', '\n', '\r', '\t', '-' };

		static CategoryMapping()
		{
			/*taxonConnection = new MySqlConnection("server=127.0.0.1;uid=root;pwd=;database=ITIS;");
			taxonConnection.Open();*/

			CommonsCategoryTree.Load(Path.Combine(Configuration.DataDirectory, "category_tree.txt"));
		}

		public CategoryMapping(string filename)
			: base(filename)
		{
		}

		/// <summary>
		/// Saves out any cached category data to files.
		/// </summary>
		public static void SaveOut()
		{
			CommonsCategoryTree.Save(Path.Combine(Configuration.DataDirectory, "category_tree.txt"));
		}

		/// <summary>
		/// If the specified tag is mapped to categories, returns the categories.
		/// </summary>
		public IEnumerable<PageTitle> GetMappedCategories(string tag, TaskItemKeyString key)
		{
			MappingCategory mappedData = TryGetValue(tag);
			if (mappedData == null || mappedData.IsUnmapped)
			{
				return null;
			}

			PageTitle mappedPageTitle = PageTitle.Parse(mappedData.MappedCategory);

			// verify that the categories are good against the live database
			if (!CommonsCategoryTree.AddToTree(mappedPageTitle, 2))
			{
				Console.WriteLine("Failed to find mapped cat '" + mappedData.MappedCategory + "'.");
				mappedData.MappedCategory = "";
			}

			return new PageTitle[] { mappedPageTitle };
		}

		/// <summary>
		/// Records the tag as checked and failed to map.
		/// </summary>
		/// <returns>The complete set of mappings for the tag.</returns>
		private IEnumerable<PageTitle> MapCategory(string tag, TaskItemKeyString key)
		{
			return MapCategory(tag, PageTitle.Empty, key);
		}

		/// <summary>
		/// Records the category the specified tag has been mapped to.
		/// </summary>
		/// <returns>The complete set of mappings for the tag.</returns>
		private IEnumerable<PageTitle> MapCategory(string tag, PageTitle category, TaskItemKeyString key)
		{
			if (!category.IsEmpty)
			{
				MappingCategory mapping = TryMapValue(tag, key);
				if (mapping.IsUnmapped)
				{
					mapping.MappedCategory = category.ToString();
				}
				else
				{
					throw new NotImplementedException("Mapping multiple categories to the same tag.");
				}

				Console.WriteLine($"Mapped '{tag}' to '{category}'.");
				yield return category;
			}
			else
			{
				// record blank categories so we don't check them again
				MappingCategory mapping = TryMapValue(tag, key);
				if (mapping.IsUnmapped)
				{
					yield break;
				}
				else
				{
					yield return PageTitle.Parse(mapping.MappedCategory);
				}
			}
		}

		/// <summary>
		/// Translates a location string into a set of categories.
		/// </summary>
		public IEnumerable<PageTitle> TranslateLocationCategory(string input, TaskItemKeyString key)
		{
			if (string.IsNullOrEmpty(input)) return null;

			IEnumerable<PageTitle> existingMapping = GetMappedCategories(input, key);
			if (existingMapping != null)
			{
				return existingMapping;
			}

			//simple mapping
			PageTitle category = TryFetchCategoryName(GlobalAPIs.Commons, input);
			if (!category.IsEmpty)
			{
				return MapCategory(input, category, key);
			}

			string[] pieces = input.Split(StringUtility.DashDash, StringSplitOptions.RemoveEmptyEntries);

			for (int c = 0; c < pieces.Length; c++)
			{
				pieces[c] = pieces[c].Replace("(state)", "").Trim();
				pieces[c] = pieces[c].Replace("(State)", "").Trim();
			}

			Console.WriteLine("Attempting to map location '" + input + "'.");

			if (pieces.Length >= 2)
			{
				//try flipping last two, with a comma
				string attempt = pieces[pieces.Length - 1].Trim() + ", " + pieces[pieces.Length - 2].Trim();
				category = TryFetchCategoryName(GlobalAPIs.Commons, attempt);
				if (!category.IsEmpty)
				{
					return MapCategory(input, category, key);
				}
			}

			//check for only the last on wikidata
			IEnumerable<QId> entities = GlobalAPIs.Wikidata.SearchEntities(pieces.Last());
			foreach (QId qid in entities.Take(5))
			{
				Entity place = GlobalAPIs.Wikidata.GetEntity(qid);

				//get country for place
				if (place.HasClaim("P17"))
				{
					//TODO: instead, verify ALL pieces are present
					throw new Exception();

					IEnumerable<Entity> parents = place.GetClaimValuesAsEntities("P17", GlobalAPIs.Wikidata);
					if (place.HasClaim("P131"))
					{
						parents = parents.Concat(place.GetClaimValuesAsEntities("P131", GlobalAPIs.Wikidata));
					}
					foreach (Entity parent in parents)
					{
						//look for the parent in the earlier pieces
						for (int c = 0; c < pieces.Length - 1; c++)
						{
							bool countrySuccess = false;
							if (parent.aliases != null && parent.aliases.ContainsKey("en"))
							{
								foreach (string s in parent.aliases["en"])
								{
									if (string.Compare(pieces[c], s, true) == 0)
									{
										countrySuccess = true;
									}
								}
							}
							if (parent.labels != null && parent.labels.ContainsKey("en")
								&& string.Compare(pieces[c], parent.labels["en"], true) == 0)
							{
								countrySuccess = true;
							}

							if (countrySuccess)
							{
								// find Commons cat from the Wikidata entity
								if (place.HasClaim(Wikidata.Prop_CommonsCategory))
								{
									string commonsCat = place.GetClaimValueAsString(Wikidata.Prop_CommonsCategory);
									return MapCategory(input, new PageTitle(PageTitle.NS_Category, commonsCat), key);
								}
								else
								{
									return MapCategory(input, key);
								}
							}
						}
					}
				}
			}

			return MapCategory(input, key);
		}

		private static HashSet<string> s_personsFailed = new HashSet<string>();

		public IEnumerable<PageTitle> TranslatePersonCategory(string input, TaskItemKeyString key, bool allowFailedCreators)
		{
			if (string.IsNullOrEmpty(input))
			{
				return null;
			}

			IEnumerable<PageTitle> existingMapping = GetMappedCategories(input, key);
			if (existingMapping != null)
			{
				return existingMapping;
			}

			if (s_personsFailed.Contains(input))
			{
				if (allowFailedCreators)
					return new PageTitle[] { ForceCategory(input) };
				else
					return MapCategory(input, key);
			}

			//simple mapping
			PageTitle category = TryFetchCategoryName(GlobalAPIs.Commons, input);
			if (!category.IsEmpty)
			{
				return MapCategory(input, category, key);
			}

			Console.WriteLine("Attempting to map person '{0}'.", input);

			//If they have a creator template, use that
			CreatorTemplate creator = CreatorUtility.GetCreatorTemplate(input); //TODO: this doesn't map strings any more
			if (!creator.IsEmpty)
			{
				PageTitle creatorPage = creator.Template;
				PageTitle homeCategory;
				if (CreatorUtility.TryGetHomeCategory(creatorPage, out homeCategory))
				{
					return MapCategory(input, homeCategory, key);
				}
				else
				{
					//try to find creator template's homecat param
					CreatorData creatorData = WikidataCache.GetCreatorData(creatorPage);
					if (creatorData != null && !creatorData.CommonsCategory.IsEmpty)
					{
						CreatorUtility.SetHomeCategory(creatorPage, creatorData.CommonsCategory);
						return MapCategory(input, creatorData.CommonsCategory, key);
					}

					//failed to find homecat, use name
					PageTitle cat = new PageTitle(PageTitle.NS_Category, creatorPage.Name);
					CreatorUtility.SetHomeCategory(creatorPage, cat);
					return MapCategory(input, cat, key);
				}
			}

			s_personsFailed.Add(input);
			if (allowFailedCreators)
				return new PageTitle[] { ForceCategory(input) };
			else
				return MapCategory(input, key);
		}

		public IEnumerable<PageTitle> TranslateTagCategory(string input, TaskItemKeyString key)
		{
			if (string.IsNullOrWhiteSpace(input)) return null;

			input = input.Trim();

			IEnumerable<PageTitle> existingMapping = GetMappedCategories(input, key);
			if (existingMapping != null)
			{
				return existingMapping;
			}

			//does it look like it has a parent?
			if (input.Contains('(') && input.EndsWith(")"))
			{
				//TODO: make me faster
				/*string switched = "";
				for (int c = input.Length - 1; c >= 0; c--)
				{
					if (input[c] == '(')
					{
						string inParen = input.Substring(c + 1, input.Length - c - 2);
						switched = inParen.Trim() + "--" + input.Substring(0, c).Trim();
						break;
					}
				}
				if (!string.IsNullOrEmpty(switched))
				{
					IEnumerable<string> location = TranslateLocationCategory(switched);
					if (location != null)
					{
						foreach (string mapped in location)
						{
							MapCategory(input, mapped);
						}
						return s_categoryMap[input].Cats;
					}
				}*/
			}

			//try to create a mapping
			Console.WriteLine("Attempting to map tag '" + input + "'.");
			return MapCategory(input, TranslateCategory(GlobalAPIs.Commons, input, key), key);
		}

		private static string[] seperatorReplacements = new string[] { " in ", " of " };

		public PageTitle TranslateCategory(Api api, string input, TaskItemKeyString key)
		{
			input = input.Trim(s_trim);

			// fix case
			if (input.IsAllLower())
			{
				input = input.ToTitleCase();
			}

			// Check if this category exists literally
			{
				PageTitle catname = TryMapCategoryFinal(api, input, true);
				if (!catname.IsEmpty) return catname;
			}

			// Try only first letter capital, for scientific names
			string firstCap = input.ToLower().ToUpperFirst();
			if (firstCap != input)
			{
				PageTitle catname = TryMapCategoryFinal(api, firstCap, true);
				if (!catname.IsEmpty) return catname;
			}

			//TOPIC--BIG--SMALL
			//TOPIC--LOC
			//BIG--SMALL--SMALLER
			//BIG--SMALL--SMALLER--SMALLEST

			//Try replacing '--' with some prepositions
			if (input.Contains("--"))
			{
				string[] split = input.Split(new string[] { "--" }, StringSplitOptions.RemoveEmptyEntries);

				for (int c = 0; c < split.Length; c++)
				{
					if (!split[c].StartsWith("Washington", StringComparison.CurrentCultureIgnoreCase))
					{
						split[c] = split[c].Replace("(state)", "").Trim();
						split[c] = split[c].Replace("(State)", "").Trim();
					}
				}

				List<string> catChecks = new List<string>();

				foreach (string s in seperatorReplacements)
				{
					catChecks.Add(string.Join(s, split));

					for (int i = split.Length - 1; i > 0; i--)
					{
						catChecks.Add(split[0] + s + split[i]);
					}
				}

				//Exact match on first word
				catChecks.Add(split[0]);

				PageTitle catname = TryMapCategoryFinal(api, catChecks, false);
				if (!catname.IsEmpty) return catname;
			}
			else
			{
				string[] pluralizations = new string[] { input + "s", input + "es" };

				PageTitle catname = TryMapCategoryFinal(api, pluralizations, false);
				if (!catname.IsEmpty) return catname;
			}

			//Failed to map
			return PageTitle.Empty;
		}

		private PageTitle TryMapCategoryFinal(Api api, IList<string> cats, bool couldBeAnimal)
		{
			//remove dupe spaces
			for (int i = 0; i < cats.Count; i++)
			{
				cats[i] = string.Join(" ", cats[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
			}

			foreach (Article resultArt in TryFetchCategories(api, cats.Select(c => ForceCategory(c))))
			{
				if (!Article.IsNullOrMissing(resultArt))
				{
					return resultArt.title;
				}
			}

			return PageTitle.Empty;
		}

		private PageTitle TryMapCategoryFinal(Api api, string cat, bool couldBeAnimal)
		{
			//remove dupe spaces
			cat = string.Join(" ", cat.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

			Article resultArt = TryFetchCategory(api, ForceCategory(cat));
			if (resultArt != null && !resultArt.missing)
			{
				return resultArt.title;
			}

			return PageTitle.Empty;

			/*if (!couldBeAnimal)
				return "";

			int tsn = -1;

			//map vernacular name to scientific name
			MySqlCommand command = new MySqlCommand("SELECT tsn FROM vernaculars WHERE vernacular_name=@incat;", taxonConnection);
			command.Parameters.AddWithValue("@incat", cat);
			tsn = -1;
			using (MySqlDataReader reader = command.ExecuteReader())
			{
				if (reader.Read()) tsn = reader.GetInt32("tsn");
			}

			if (tsn >= 0)
			{
				//get name
				string name = GetLongname(tsn);
				if (!string.IsNullOrEmpty(name))
				{
					scientific = name;

					Wikimedia.Article sciCat = TryFetchCategory(api, name);
					if (sciCat != null && !sciCat.missing)
					{
						return sciCat.title;
					}
				}
			}

			//TODO: allow an error distance of 1

			tsn = -1;

			//look up longname
			command = new MySqlCommand("SELECT tsn FROM longnames WHERE completename=@incat;", taxonConnection);
			command.Parameters.AddWithValue("@incat", cat);
			using (MySqlDataReader reader = command.ExecuteReader())
			{
				if (reader.Read()) tsn = reader.GetInt32("tsn");
			}

			//map to accepted scientific name if possible
			command = new MySqlCommand("SELECT tsn_accepted AS tsn FROM synonym_links WHERE tsn=@oldtsn;", taxonConnection);
			command.Parameters.AddWithValue("@oldtsn", tsn);
			using (MySqlDataReader reader = command.ExecuteReader())
			{
				if (reader.Read()) tsn = reader.GetInt32("tsn");
			}

			if (tsn >= 0)
			{
				//get name
				string name = GetLongname(tsn);
				if (!string.IsNullOrEmpty(name))
				{
					scientific = name;

					Wikimedia.Article sciCat = TryFetchCategory(api, name);
					if (sciCat != null && !sciCat.missing)
					{
						return sciCat.title;
					}
				}
			}

			//If we found a scientific name, use it even if the cat doesn't exist
			if (!string.IsNullOrEmpty(scientific))
			{
				if (!scientific.StartsWith("Category:")) scientific = "Category:" + scientific;
				return scientific;
			}

			return "";*/
		}

		private PageTitle ForceCategory(string pageName)
		{
			PageTitle title = PageTitle.Parse(pageName);
			title.Namespace = PageTitle.NS_Category;
			return title;
		}

		/// <summary>
		/// Get a long name from the taxonomy database.
		/// </summary>
		private string GetLongname(int tsn)
		{
			/*MySqlCommand command = new MySqlCommand("SELECT completename FROM longnames WHERE tsn=@itsn", taxonConnection);
			command.Parameters.AddWithValue("@itsn", tsn);
			using (MySqlDataReader reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					return reader.GetString("completename").Trim();
				}
			}*/
			return "";
		}

		public PageTitle TryFetchCategoryName(Api api, string cat)
		{
			PageTitle title = PageTitle.Parse(cat);
			title.Namespace = PageTitle.NS_Category;
			Article article = TryFetchCategory(api, title);
			if (!Article.IsNullOrMissing(article))
			{
				return article.title;
			}
			else
			{
				return PageTitle.Empty;
			}
		}

		/// <summary>
		/// Look for the specified categories. Return the actual categories used.
		/// </summary>
		public IEnumerable<Article> TryFetchCategories(Api api, IEnumerable<PageTitle> cats)
		{
			// don't request cats we already know about
			List<PageTitle> requestCats = new List<PageTitle>();
			foreach (PageTitle inCat in cats)
			{
				if (!s_extantCategories.ContainsKey(inCat))
				{
					requestCats.AddUnique(inCat);
				}
			}

			// request and map cats
			Article[] arts = GetCategories(api, requestCats).ToArray();
			for (int i = 0; i < arts.Length; i++)
			{
				if (arts[i] != null && !arts[i].missing)
				{
					Article fullyRedirectedArticle = ProcessCategoryRedirects(api, arts[i]);

					// category might have already been processed as part of a redirect chain
					if (!s_extantCategories.ContainsKey(requestCats[i]))
					{
						s_extantCategories.Add(requestCats[i], fullyRedirectedArticle);
					}
				}
				else
				{
					s_extantCategories.Add(requestCats[i], null);
				}
			}

			// collect the full list (previously- and freshly-mapped ones)
			foreach (PageTitle category in cats)
			{
				Article article = s_extantCategories[category];
				if (!Article.IsNullOrMissing(article))
				{
					yield return article;
				}
				else
				{
					yield return null;
				}
			}
		}

		/// <summary>
		/// Look for the specified category. Return the actual category used.
		/// </summary>
		public Article TryFetchCategory(Api api, PageTitle cat)
		{
			Article extant;
			if (s_extantCategories.TryGetValue(cat, out extant))
			{
				return extant;
			}

			Console.WriteLine("FETCH: '" + cat + "'");

			Article art = GetCategory(api, ref cat);
			art = ProcessCategoryRedirects(api, art);
			s_extantCategories[cat] = art;
			return art;
		}

		/// <summary>
		/// If the specified article is a category redirect, returns the article redirected to.
		/// </summary>
		private Article ProcessCategoryRedirects(Api api, Article art)
		{
			if (art != null)
			{
				string arttext = art.revisions[0].text;

				//Check for category redirect
				string template = WikiUtils.ExtractTemplate(arttext, "category redirect");
				if (string.IsNullOrEmpty(template))
				{
					template = WikiUtils.ExtractTemplate(arttext, "Category Redirect");
				}
				if (string.IsNullOrEmpty(template))
				{
					template = WikiUtils.ExtractTemplate(arttext, "seecat");
				}

				if (!string.IsNullOrEmpty(template))
				{
					//found redirect, look it up
					string cat = WikiUtils.GetTemplateParameter(1, template);
					PageTitle catTitle = PageTitle.Parse(cat);
					catTitle.Namespace = PageTitle.NS_Category;
					//TODO: check for redirect loops
					Article redir = TryFetchCategory(api, catTitle);
					return redir;
				}
				else
				{
					//no redirect, just return
					return art;
				}
			}
			else
			{
				return null;
			}
		}

		private static IEnumerable<Article> GetCategories(Api api, IList<PageTitle> cats)
		{
			foreach (PageTitle cat in cats)
			{
				if (cat.Namespace != PageTitle.NS_Category)
				{
					throw new ArgumentException("Provided title is not in the Category namespace.");
				}
			}
			return api.GetPages(cats);
		}

		private static Article GetCategory(Api api, ref PageTitle cat)
		{
			if (cat.Namespace != PageTitle.NS_Category)
			{
				throw new ArgumentException("Provided title is not in the Category namespace.");
			}
			Article art = api.GetPage(cat);
			if (!Article.IsNullOrMissing(art))
			{
				return art;
			}
			else
			{
				return null;
			}

			/*content = "";
			MySqlCommand command = new MySqlCommand("SELECT * FROM category WHERE cat_title=@title", commonsConnection);
			command.Parameters.AddWithValue("@title", cat.Replace(' ', '_'));
			using (MySqlDataReader reader = command.ExecuteReader())
			{
				if (reader.Read())
				{
					cat = "Category:" + reader.GetString("cat_title").Replace("_", " ");
					return true;
				}
				else
				{
					return false;
				}
			}

			content = "";
			return false;*/
		}
	}
}
