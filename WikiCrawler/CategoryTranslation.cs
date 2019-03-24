using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WikiCrawler
{
	/// <summary>
	/// Helper functions for translating raw content into categories.
	/// </summary>
	public static class CategoryTranslation
	{
		//private static MySqlConnection taxonConnection;

		private static Wikimedia.WikiApi s_commonsApi = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));
		private static Wikimedia.WikiApi s_wikidataApi = new Wikimedia.WikiApi(new Uri("http://wikidata.org/"));

		/// <summary>
		/// Cached tree structure of Commons categories.
		/// </summary>
		public static readonly Wikimedia.CategoryTree CategoryTree = new Wikimedia.CategoryTree(s_commonsApi);

		private class CategoryMappingData
		{
			public List<string> Cats = new List<string>();

			public int Usage = 1;

			public CategoryMappingData()
			{

			}

			public CategoryMappingData(string category)
			{
				Cats = new List<string>();
				AddCategory(category);
				Usage = 0;
			}

			public void AddCategory(string category)
			{
				if (!string.IsNullOrEmpty(category) && !Cats.Contains(category))
				{
					Cats.Add(category);
				}
			}
		}

		/// <summary>
		/// Tags mapped to Commons categories.
		/// </summary>
		private static Dictionary<string, CategoryMappingData> s_categoryMap = new Dictionary<string, CategoryMappingData>(StringComparer.InvariantCultureIgnoreCase);
		
		private static char[] s_trim = new char[] { ' ', '\n', '\r', '\t', '-' };

		static CategoryTranslation()
		{
			/*taxonConnection = new MySqlConnection("server=127.0.0.1;uid=root;pwd=;database=ITIS;");
			taxonConnection.Open();*/

			//load mapped categories
			string categoryMappingsFile = Path.Combine(Configuration.DataDirectory, "category_mappings.json");
			string serializedMappings = File.ReadAllText(categoryMappingsFile, Encoding.UTF8);
			s_categoryMap = JsonConvert.DeserializeObject<Dictionary<string, CategoryMappingData>>(serializedMappings);

			// clear usage
			foreach (KeyValuePair<string, CategoryMappingData> kv in s_categoryMap)
			{
				kv.Value.Usage = 0;
			}

			CategoryTree.Load(Path.Combine(Configuration.DataDirectory, "category_tree.txt"));
		}

		/// <summary>
		/// Saves out any cached category data to files.
		/// </summary>
		public static void SaveOut()
		{
			//write category map
			string categoryMappingsFile = Path.Combine(Configuration.DataDirectory, "category_mappings.json");
			using (StreamWriter writer = new StreamWriter(new FileStream(categoryMappingsFile, FileMode.Create), Encoding.UTF8))
			{
				List<KeyValuePair<string, CategoryMappingData>> flatMap = s_categoryMap.ToList();

				writer.WriteLine("{");
				bool isFirst = true;
				foreach (KeyValuePair<string, CategoryMappingData> kv in flatMap.OrderByDescending(kv => kv.Value.Usage))
				{
					if (!isFirst)
					{
						writer.WriteLine(",");
					}
					writer.WriteLine("\t" + JsonConvert.ToString(kv.Key) + ": { \"Usage\":" + kv.Value.Usage.ToString() + ",");
					string cats = JsonConvert.SerializeObject(kv.Value.Cats);
					writer.Write("\t\t\"Cats\": " + cats + "}");
					isFirst = false;
				}
				writer.WriteLine("}");
			}

			CategoryTree.Save(Path.Combine(Configuration.DataDirectory, "category_tree.txt"));
		}

		/// <summary>
		/// If the specified tag is mapped to categories, returns the categories.
		/// </summary>
		public static IEnumerable<string> GetMappedCategories(string tag)
		{
			CategoryMappingData mappedData;
			if (s_categoryMap.TryGetValue(tag, out mappedData))
			{
				//verify that the categories are good against the live database
				for (int i = 0; i < mappedData.Cats.Count; i++)
				{
					if (!CategoryTree.AddToTree(mappedData.Cats[i], 2))
					{
						Console.WriteLine("Failed to find mapped cat '" + mappedData.Cats[i] + "'.");
						mappedData.Cats.RemoveAt(i);
					}
				}

				mappedData.Usage++;
				return mappedData.Cats;
			}
			else
			{
				//flag this category as needing manual mapping
				s_categoryMap[tag] = new CategoryMappingData();
				return null;
			}
		}

		/// <summary>
		/// Records the tag as checked and failed to map.
		/// </summary>
		/// <returns>The complete set of mappings for the tag.</returns>
		private static IEnumerable<string> MapCategory(string tag)
		{
			return MapCategory(tag, "");
		}

		/// <summary>
		/// Records the category the specified tag has been mapped to.
		/// </summary>
		/// <returns>The complete set of mappings for the tag.</returns>
		private static IEnumerable<string> MapCategory(string tag, string category)
		{
			if (!string.IsNullOrEmpty(category))
			{
				if (!category.StartsWith("Category:")) category = "Category:" + category;
				CategoryMappingData categoryData;
				if (s_categoryMap.TryGetValue(tag, out categoryData))
				{
					categoryData.AddCategory(category);
				}
				else
				{
					categoryData = new CategoryMappingData(category);
					s_categoryMap.Add(tag, categoryData);
				}
				Console.WriteLine("Mapped '" + tag + "' to '" + category + "'.");
				return categoryData.Cats;
			}
			else
			{
				// record blank categories so we don't check them again
				CategoryMappingData categoryData;
				if (!s_categoryMap.TryGetValue(tag, out categoryData))
				{
					categoryData = new CategoryMappingData();
					s_categoryMap.Add(tag, categoryData);
				}
				return categoryData.Cats;
			}
		}

		/// <summary>
		/// Translates a location string into a set of categories.
		/// </summary>
		public static IEnumerable<string> TranslateLocationCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return null;

			IEnumerable<string> existingMapping = GetMappedCategories(input);
			if (existingMapping != null)
			{
				return existingMapping;
			}

			//simple mapping
			string category = TryFetchCategoryName(s_commonsApi, input);
			if (!string.IsNullOrEmpty(category))
			{
				return MapCategory(input, category);
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
				category = TryFetchCategoryName(s_commonsApi, attempt);
				if (!string.IsNullOrEmpty(category))
				{
					return MapCategory(input, category);
				}
			}

			//check for only the last on wikidata
			string[] entities = s_wikidataApi.SearchEntities(pieces.Last());
			for (int d = 0; d < Math.Min(5, entities.Length); d++)
			{
				Wikimedia.Entity place = s_wikidataApi.GetEntity(entities[d]);

				//get country for place
				if (place.HasClaim("P17"))
				{
					IEnumerable<Wikimedia.Entity> parents = place.GetClaimValuesAsEntity("P17", s_wikidataApi);
					if (place.HasClaim("P131"))
					{
						parents = parents.Concat(place.GetClaimValuesAsEntity("P131", s_wikidataApi));
					}
					foreach (Wikimedia.Entity parent in parents)
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
								if (place.HasClaim("P373"))
								{
									return MapCategory(input, place.GetClaimValueAsString("P373"));
								}
								else
								{
									return MapCategory(input);
								}
							}
						}
					}
				}
			}

			return MapCategory(input);
		}

		private static HashSet<string> s_personsFailed = new HashSet<string>();

		public static IEnumerable<string> TranslatePersonCategory(string input, bool allowFailedCreators)
		{
			if (string.IsNullOrEmpty(input)) return null;

			IEnumerable<string> existingMapping = GetMappedCategories(input);
			if (existingMapping != null)
			{
				return existingMapping;
			}

			if (s_personsFailed.Contains(input))
			{
				if (allowFailedCreators)
					return new string[] { input };
				else
					return MapCategory(input);
			}

			//simple mapping
			string category = TryFetchCategoryName(s_commonsApi, input);
			if (!string.IsNullOrEmpty(category))
			{
				return MapCategory(input, category);
			}

			Console.WriteLine("Attempting to map person '" + input + "'.");

			//make sure creator is created
			Creator creator;

			//If they have a creator template, use that
			if (CreatorUtility.TryGetCreator(input, out creator))
			{
				if (!string.IsNullOrEmpty(creator.Author))
				{
					string creatorPage = creator.Author;
					creatorPage = creatorPage.Trim('{').Trim('}');
					string homeCategory;
					if (CreatorUtility.TryGetHomeCategory(creatorPage, out homeCategory))
					{
						return MapCategory(input, homeCategory);
					}
					else
					{
						//try to find creator template's homecat param
						Wikimedia.Article creatorArticle = s_commonsApi.GetPage(creatorPage);
						if (creatorArticle != null && !creatorArticle.missing)
						{
							foreach (string s in creatorArticle.revisions[0].text.Split('|'))
							{
								if (s.TrimStart().StartsWith("homecat", StringComparison.InvariantCultureIgnoreCase))
								{
									string homecat = s.Split(StringUtility.Equal, 2)[1].Trim();
									if (!string.IsNullOrEmpty(homecat))
									{
										if (!homecat.StartsWith("Category:")) homecat = "Category:" + homecat;
										CreatorUtility.SetHomeCategory(creatorPage, homecat);
										return MapCategory(input, homecat);
									}
								}
							}
						}

						//failed to find homecat, use name
						string creatorName = creatorPage;
						if (creatorName.StartsWith("Creator:")) creatorName = creatorName.Substring(8);
						string cat = "Category:" + creatorName;
						CreatorUtility.SetHomeCategory(creatorPage, cat);
						return MapCategory(input, cat);
					}
				}
				else
				{
					return MapCategory(input);
				}
			}

			s_personsFailed.Add(input);
			if (allowFailedCreators)
				return new string[] { input };
			else
				return MapCategory(input);
		}

		public static IEnumerable<string> TranslateTagCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return null;

			IEnumerable<string> existingMapping = GetMappedCategories(input);
			if (existingMapping != null)
			{
				return existingMapping;
			}

			input = input.Trim();

			//does it look like it has a parent?
			if (input.Contains('(') && input.EndsWith(")"))
			{
				string switched = "";
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
				}
			}

			//try to create a mapping
			Console.WriteLine("Attempting to map tag '" + input + "'.");
			return MapCategory(input, TranslateCategory(s_commonsApi, input));
		}

		public static string TranslateCategory(Wikimedia.WikiApi api, string input)
		{
			string catname = input.Trim(s_trim);

			//Check if this category exists literally
			catname = TryMapCategory0(api, input);
			if (!string.IsNullOrEmpty(catname)) return catname;

			//Failed to map
			return "";
		}

		private static string[] seperatorReplacements = new string[] { " in ", " of " };

		private static string TryMapCategory0(Wikimedia.WikiApi api, string input)
		{
			string catname = TryMapCategoryFinal(api, input, true);
			if (!string.IsNullOrEmpty(catname)) return catname;

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

				foreach (string s in seperatorReplacements)
				{
					catname = TryMapCategoryFinal(api, string.Join(s, split), false);
					if (!string.IsNullOrEmpty(catname)) return catname;

					for (int i = split.Length - 1; i > 0; i--)
					{
						catname = TryMapCategoryFinal(api, split[0] + s + split[i], false);
						if (!string.IsNullOrEmpty(catname)) return catname;
					}
				}

				//Exact match on first word
				catname = TryMapCategoryFinal(api, split[0], false);
				if (!string.IsNullOrEmpty(catname)) return catname;
			}

			//Failed to map
			return "";
		}

		private static string TryMapCategoryFinal(Wikimedia.WikiApi api, string cat, bool couldBeAnimal)
		{
			string scientific = "";

			//remove dupe spaces
			cat = string.Join(" ", cat.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

			Wikimedia.Article resultArt = TryFetchCategory(api, cat);
			if (resultArt != null && !resultArt.missing)
			{
				return resultArt.title;
			}

			return "";

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

		/// <summary>
		/// Get a long name from the taxonomy database.
		/// </summary>
		private static string GetLongname(int tsn)
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

		public static string TryFetchCategoryName(Wikimedia.WikiApi api, string cat)
		{
			Wikimedia.Article article = TryFetchCategory(api, cat);
			if (article != null && !article.missing)
				return article.title;
			else
				return "";
		}

		/// <summary>
		/// Look for the specified category. Return the actual category used.
		/// </summary>
		public static Wikimedia.Article TryFetchCategory(Wikimedia.WikiApi api, string cat)
		{
			if (!cat.StartsWith("Category:")) cat = "Category:" + cat;

			Console.WriteLine("FETCH: '" + cat + "'");

			Wikimedia.Article art = GetCategory(api, ref cat);
			if (art != null)
			{
				string arttext = art.revisions[0].text;

				//Check for category redirect
				string catRedir1 = "category redirect";
				string catRedir2 = "seecat";
				int redir1 = arttext.IndexOf(catRedir1, StringComparison.InvariantCultureIgnoreCase);
				int redir2 = arttext.IndexOf(catRedir2, StringComparison.InvariantCultureIgnoreCase);
				int catstart = -1;
				if (redir1 >= 0)
				{
					catstart = redir1 + catRedir1.Length + 1;
				}
				else if (redir2 >= 0)
				{
					catstart = redir2 + catRedir2.Length + 1;
				}

				if (catstart >= 0)
				{
					//found redirect, look it up
					cat = arttext.Substring(catstart, arttext.IndexOf("}}", catstart) - catstart);
					cat = cat.Split('|')[0];
					if (cat.Contains("=")) cat = cat.Split('=')[1];
					return TryFetchCategory(api, cat);
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

		private static Wikimedia.Article GetCategory(Wikimedia.WikiApi api, ref string cat)
		{
			Wikimedia.Article art = api.GetPage(cat);
			if (art != null && !art.missing)
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
