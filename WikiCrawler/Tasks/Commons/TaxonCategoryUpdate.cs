using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;

namespace Tasks.Commons
{
	public class TaxonCategoryUpdate : BaseTask
	{
		private static Api SpeciesApi = new Api(new Uri("http://species.wikimedia.org/"));

		private const string taxonTodoFile = "taxon_todo.txt";
		private const string catTreeFile = "taxon_category_todo.txt";
		private static List<PageTitle> todoCategories = new List<PageTitle>();
		private static Dictionary<PageTitle, List<PageTitle>> catTree = new Dictionary<PageTitle, List<PageTitle>>();

		//private static MySqlConnection itisConnection;

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

		public override void Execute()
		{
			// read todo list
			using (StreamReader reader = new StreamReader(new FileStream(taxonTodoFile, FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					todoCategories.Add(PageTitle.Parse(reader.ReadLine()));
				}
			}

			// read category tree
			using (StreamReader reader = new StreamReader(new FileStream(catTreeFile, FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					PageTitle key = PageTitle.Parse(reader.ReadLine());
					int count = int.Parse(reader.ReadLine());
					List<PageTitle> children = new List<PageTitle>();
					catTree[key] = children;
					for (int c = 0; c < count; c++)
					{
						children.Add(PageTitle.Parse(reader.ReadLine()));
					}
				}
			}

			//itisConnection = new MySqlConnection("server=127.0.0.1;uid=root;pwd=;database=ITIS;");
			//itisConnection.Open();

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
					foreach (PageTitle s in todoCategories)
					{
						writer.WriteLine(s);
					}
				}

				//save out cat tree
				using (StreamWriter writer = new StreamWriter(new FileStream(catTreeFile, FileMode.Create)))
				{
					foreach (KeyValuePair<PageTitle, List<PageTitle>> kv in catTree)
					{
						writer.WriteLine(kv.Key);
						writer.WriteLine(kv.Value.Count);
						foreach (PageTitle s in kv.Value)
						{
							writer.WriteLine(s);
						}
					}
				}
			}
		}

		private static void UpdateCategory(PageTitle catname)
		{
			// add to category tree
			if (!catTree.ContainsKey(catname))
			{
				List<PageTitle> children = new List<PageTitle>();
				foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(catname, cmtype: CMType.subcat))
				{
					children.Add(article.title);
				}
				catTree[catname] = children;
			}

			// update page text
			Article thisArticle = GlobalAPIs.Commons.GetPage(catname);

		}
	}
}
