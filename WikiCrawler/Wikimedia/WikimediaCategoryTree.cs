using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;

namespace Wikimedia
{
	class CategoryTree
	{
		private static Dictionary<string, List<string>> s_CategoriesByCategory;

		private readonly WikiApi Api;

		public CategoryTree(WikiApi api)
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
		/// <param name="cat"></param>
		/// <returns>False if the category does not exist.</returns>
		public bool AddToTree(string cat, int maxdepth)
		{
			if (!cat.StartsWith("Category:")) cat = "Category:" + cat;

			if (s_CategoriesByCategory.ContainsKey(cat)) return true;

			Queue<KeyValuePair<Article, int>> queue = new Queue<KeyValuePair<Article, int>>();

			Console.WriteLine("Begin checking cats: " + cat);
			Article catArt = WikiCrawler.CategoryTranslation.TryFetchCategory(Api, cat);
			if (catArt == null || catArt.revisions == null || catArt.revisions.Length == 0) return false;

			queue.Enqueue(new KeyValuePair<Article, int>(catArt, 0));

			while (queue.Count > 0)
			{
				KeyValuePair<Article, int> kv = queue.Dequeue();
				Article check = kv.Key;
				if (check == null) continue;

				cat = check.title;

				if (s_CategoriesByCategory.ContainsKey(cat)) continue;
				if (check.revisions == null || check.revisions.Length == 0) continue;

				Console.WriteLine("Checking cats: " + cat);
				s_CategoriesByCategory[cat] = WikiUtils.GetCategories(check.revisions[0].text).ToList();
				if (s_CategoriesByCategory[cat].Count > 0)
				{
					if (kv.Value < maxdepth)
					{
						foreach (Article art in Api.GetPages(s_CategoriesByCategory[cat]))
							queue.Enqueue(new KeyValuePair<Article, int>(art, kv.Value + 1));
					}
					else
					{
						Console.WriteLine("Reached max depth.");
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Removes any categories from the list that are just less-specific versions of other categories.
		/// </summary>
		public void RemoveLessSpecific(IList<string> categories)
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
	}
}
