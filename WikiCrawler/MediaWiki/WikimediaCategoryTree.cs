using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;

namespace MediaWiki
{
	public class CategoryTree
	{
		/// <summary>
		/// For each category, its parent categories.
		/// </summary>
		private static Dictionary<PageTitle, List<PageTitle>> s_CategoriesByCategory;

		private readonly Api Api;

		public CategoryTree(Api api)
		{
			Api = api;
		}

		public void Load(string file)
		{
			s_CategoriesByCategory = new Dictionary<PageTitle, List<PageTitle>>();
			if (File.Exists(file))
			{
				using (StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open), Encoding.Default))
				{
					while (!reader.EndOfStream)
					{
						PageTitle cat = PageTitle.Parse(reader.ReadLine());
						if (cat.IsEmpty) continue;

						s_CategoriesByCategory[cat] = new List<PageTitle>();

						int count = int.Parse(reader.ReadLine());
						for (int c = 0; c < count; c++)
						{
							s_CategoriesByCategory[cat].Add(PageTitle.Parse(reader.ReadLine()));
						}
					}
				}
			}
		}

		public void Save(string file)
		{
			using (StreamWriter writer = new StreamWriter(new FileStream(file, FileMode.Create), Encoding.Default))
			{
				foreach (KeyValuePair<PageTitle, List<PageTitle>> kv in s_CategoriesByCategory)
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rootCat"></param>
		/// <returns>False if the category does not exist.</returns>
		public bool AddToTree(PageTitle rootCat, int maxdepth)
		{
			if (s_CategoriesByCategory.ContainsKey(rootCat)) return true;
			
			Console.WriteLine("Begin checking cats: " + rootCat);
			Article rootArticle = Api.GetPage(rootCat, prop: Prop.categories, rvprop: "");
			if (rootArticle == null || rootArticle.categories == null || rootArticle.categories.Length == 0) return false;

			List<Article> queuedCats = new List<Article>();
			queuedCats.AddRange(rootArticle.categories);
			s_CategoriesByCategory[rootArticle.title] = rootArticle.categories.Select(art => art.title).ToList();

			int depth = 0;
			while (++depth < maxdepth)
			{
				List<Article> newQueuedCats = new List<Article>();
				foreach (Article queuedArt in Api.GetPages(queuedCats, prop: Prop.categories, rvprop: ""))
				{
					if (queuedArt.missing)
					{
						s_CategoriesByCategory[queuedArt.title] = new List<PageTitle>();
						continue;
					}

					s_CategoriesByCategory[queuedArt.title] = queuedArt.categories.Select(art => art.title).ToList();

					Console.WriteLine("Checking cats: " + queuedArt.title);
					foreach (Article subcat in queuedArt.categories)
					{
						if (s_CategoriesByCategory.ContainsKey(subcat.title)) continue;
						newQueuedCats.Add(subcat);
					}
				}
				queuedCats = newQueuedCats;
			}

			Console.WriteLine("Reached max depth.");
			return true;
		}

		/// <summary>
		/// Removes any categories from the list that are just less-specific versions of other categories.
		/// </summary>
		public void RemoveLessSpecific(ISet<PageTitle> categories)
		{
			foreach (PageTitle s in categories)
				AddToTree(s, 2);

			Queue<PageTitle> toCheck = new Queue<PageTitle>();
			HashSet<PageTitle> alreadyChecked = new HashSet<PageTitle>();
			foreach (PageTitle s in categories) toCheck.Enqueue(s);

			while (toCheck.Count > 0)
			{
				PageTitle checkCat = toCheck.Dequeue();

				if (!s_CategoriesByCategory.ContainsKey(checkCat)) continue;

				//queue this cat's parents, and remove them from the source list if they exist
				List<PageTitle> parents = s_CategoriesByCategory[checkCat];
				for (int c = 0; c < parents.Count; c++)
				{
					PageTitle cat = parents[c];
					if (!alreadyChecked.Contains(cat))
					{
						if (categories.Remove(cat))
						{
							Console.WriteLine("Removed less specific cat '{0}'.", cat);
						}
						toCheck.Enqueue(cat);
						alreadyChecked.Add(cat);
					}
				}
			}
		}

		/// <summary>
		/// Returns true if the specified category has the parent category as a parent.
		/// </summary>
		public bool HasParent(PageTitle category, PageTitle findParent)
		{
			Queue<PageTitle> toCheck = new Queue<PageTitle>();
			HashSet<PageTitle> alreadyChecked = new HashSet<PageTitle>();
			toCheck.Enqueue(category);

			while (toCheck.Count > 0)
			{
				PageTitle checkCat = toCheck.Dequeue();

				if (!s_CategoriesByCategory.ContainsKey(checkCat)) continue;

				foreach (PageTitle parent in s_CategoriesByCategory[checkCat])
				{
					if (!alreadyChecked.Contains(parent))
					{
						if (parent == findParent)
						{
							return true;
						}
						toCheck.Enqueue(parent);
						alreadyChecked.Add(parent);
					}
				}
			}

			return false;
		}
	}
}
