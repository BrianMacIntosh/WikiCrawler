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
		private static Dictionary<string, List<string>> s_CategoriesByCategory;

		private readonly Api Api;

		public CategoryTree(Api api)
		{
			Api = api;
		}

		public void Load(string file)
		{
			s_CategoriesByCategory = new Dictionary<string, List<string>>();
			if (File.Exists(file))
			{
				using (StreamReader reader = new StreamReader(new FileStream(file, FileMode.Open), Encoding.Default))
				{
					while (!reader.EndOfStream)
					{
						string cat = reader.ReadLine();
						if (string.IsNullOrEmpty(cat)) continue;

						s_CategoriesByCategory[cat] = new List<string>();

						int count = int.Parse(reader.ReadLine());
						for (int c = 0; c < count; c++)
						{
							s_CategoriesByCategory[cat].Add(reader.ReadLine());
						}
					}
				}
			}
		}

		public void Save(string file)
		{
			using (StreamWriter writer = new StreamWriter(new FileStream(file, FileMode.Create), Encoding.Default))
			{
				foreach (KeyValuePair<string, List<string>> kv in s_CategoriesByCategory)
				{
					writer.WriteLine(kv.Key);
					writer.WriteLine(kv.Value.Count);
					foreach (string s in kv.Value)
						writer.WriteLine(s);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rootCat"></param>
		/// <returns>False if the category does not exist.</returns>
		public bool AddToTree(string rootCat, int maxdepth)
		{
			rootCat = WikiUtils.GetCategoryCategory(rootCat);

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
						s_CategoriesByCategory[queuedArt.title] = new List<string>();
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
		public void RemoveLessSpecific(ISet<string> categories)
		{
			foreach (string s in categories)
				AddToTree(s, 2);

			Queue<string> toCheck = new Queue<string>();
			HashSet<string> alreadyChecked = new HashSet<string>();
			foreach (string s in categories) toCheck.Enqueue(s);

			while (toCheck.Count > 0)
			{
				string checkCat = toCheck.Dequeue();

				if (!s_CategoriesByCategory.ContainsKey(checkCat)) continue;

				//queue this cat's parents, and remove them from the source list if they exist
				List<string> parents = s_CategoriesByCategory[checkCat];
				for (int c = 0; c < parents.Count; c++)
				{
					string cat = parents[c];
					cat = HttpUtility.UrlDecode(cat);
					if (!alreadyChecked.Contains(cat))
					{
						if (categories.Remove(cat))
						{
							Console.WriteLine("Removed less specific cat '" + cat + "'.");
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
		public bool HasParent(string category, string findParent)
		{
			Queue<string> toCheck = new Queue<string>();
			HashSet<string> alreadyChecked = new HashSet<string>();
			toCheck.Enqueue(category);

			while (toCheck.Count > 0)
			{
				string checkCat = toCheck.Dequeue();

				if (!s_CategoriesByCategory.ContainsKey(checkCat)) continue;

				foreach (string parent in s_CategoriesByCategory[checkCat])
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
