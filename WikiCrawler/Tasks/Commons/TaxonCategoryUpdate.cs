using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MySql.Data.MySqlClient;

namespace WikiCrawler
{
	class TaxonCategoryUpdate
	{
		private static Wikimedia.WikiApi Api = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));
		private static Wikimedia.WikiApi SpeciesApi = new Wikimedia.WikiApi(new Uri("http://species.wikimedia.org/"));

		private const string taxonTodoFile = "taxon_todo.txt";
		private const string catTreeFile = "taxon_category_todo.txt";
		private static List<string> todoCategories = new List<string>();
		private static Dictionary<string, List<string>> catTree = new Dictionary<string, List<string>>();

		private static MySqlConnection itisConnection;

		/*
		 * Prefer FishBase over ITIS when available
		 * 
		 * Things to check:
		 * {{VN|en=<NAME>}}
		 * {{SN|<NAMES>}}
		 * {{Taxonavigation|include=<INCLUDE>|authority=<AUTHOR>, <DATE>}}
		 * {{Wikispecies|<PAGENAME>}}
		 * 
		 * Add "2" if there are very many
		 * {{Taxa|<LEVEL>|<SUBCLASS1>|<SUBCLASS2>|source=ITIS}}
		 * {{Genera|<GENUS1>|<GENUS2>|source=<SOURCE>|accessdate=<YYYY-MM-DD>}}
		 * {{Species|<G>|<ENUS>|<SPECIES1>|<SPECIES2>|source=<SOURCE>|accessdate=<YYYY-MM-DD>}}
		 * {{Subspecies|<S>|<PECIES>|<SUB1>|<SUB2>|source=<SOURCE>|accessdate=<YYYY-MM-DD>}}
		 * 
		 * * {{FishBase ordo}}
		 * * {{FishBase family}}
		 * * {{FishBase subfamily}}
		 * * {{FishBase genus}}
		 * * {{FishBase species}}
		 * * {{ITIS|<INDEX>|''<DISPLAYNAME>'' <AUTHOR>, <YEAR>|nv}}
		 * * {{WRMS|<INDEX>|}}
		 * * {{NCBI|<INDEX>|}}
		 */

		public static void Do()
		{
			// read todo list
			using (StreamReader reader = new StreamReader(new FileStream(taxonTodoFile, FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					todoCategories.Add(reader.ReadLine());
				}
			}

			// read category tree
			using (StreamReader reader = new StreamReader(new FileStream(catTreeFile, FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					string key = reader.ReadLine();
					int count = int.Parse(reader.ReadLine());
					List<string> children = new List<string>();
					catTree[key] = children;
					for (int c = 0; c < count; c++)
					{
						children.Add(reader.ReadLine());
					}
				}
			}

			itisConnection = new MySqlConnection("server=127.0.0.1;uid=root;pwd=;database=ITIS;");
			itisConnection.Open();

			Console.WriteLine("Logging in...");
			Api.LogIn();

			try
			{
				while (todoCategories.Count > 0)
				{
					UpdateCategory(todoCategories[0]);
					todoCategories.RemoveAt(0);
				}
			}
			finally
			{
				//save out todo list
				using (StreamWriter writer = new StreamWriter(new FileStream(taxonTodoFile, FileMode.Create)))
				{
					foreach (string s in todoCategories)
					{
						writer.WriteLine(s);
					}
				}

				//save out cat tree
				using (StreamWriter writer = new StreamWriter(new FileStream(catTreeFile, FileMode.Create)))
				{
					foreach (KeyValuePair<string, List<string>> kv in catTree)
					{
						writer.WriteLine(kv.Key);
						writer.WriteLine(kv.Value.Count);
						foreach (string s in kv.Value)
						{
							writer.WriteLine(s);
						}
					}
				}
			}
		}

		private static void UpdateCategory(string catname)
		{
			// add to category tree
			if (!catTree.ContainsKey(catname))
			{
				Wikimedia.Article[] articles = Api.GetCategoryPagesFlat(catname);
				List<string> children = new List<string>();
				foreach (Wikimedia.Article article in articles)
				{
					if (article.title.StartsWith("Category:"))
					{
						children.Add(article.title);
					}
				}
				catTree[catname] = children;
			}

			// update page text
			Wikimedia.Article thisArticle = Api.GetPage(catname);

		}
	}
}
