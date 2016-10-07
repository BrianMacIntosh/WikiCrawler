using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

namespace WikiCrawler
{
	static class CategoryTranslation
	{
		//private static MySqlConnection taxonConnection;

		private static bool m_Init = false;

		private static char[] trim = new char[] { ' ', '\n', '\r', '\t', '-' };

		private static void Init()
		{
			if (m_Init) return;

			/*taxonConnection = new MySqlConnection("server=127.0.0.1;uid=root;pwd=;database=ITIS;");
			taxonConnection.Open();*/

			m_Init = true;
		}

		public static string TranslateCategory(Wikimedia.WikiApi api, string input)
		{
			Init();

			string catname = input.Trim(trim);

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
					split[c] = split[c].Replace("(state)", "").Trim();
					split[c] = split[c].Replace("(State)", "").Trim();
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
