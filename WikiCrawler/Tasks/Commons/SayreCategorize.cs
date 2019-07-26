using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WikiCrawler;

namespace Tasks
{
	public static class SayreCategorize
	{
		[BatchTask]
		public static void DoProductions()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			Api Api = new Api(commons);

			string treeFile = Path.Combine(Configuration.DataDirectory, "category_tree.txt");
			CategoryTree tree = new CategoryTree(Api);
			tree.Load(treeFile);

			string doneFile = Path.Combine(Configuration.DataDirectory, "sayercat_done.txt");
			List<string> doneFiles;
			if (File.Exists(doneFile))
			{
				doneFiles = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(doneFile, Encoding.UTF8));
			}
			else
			{
				doneFiles = new List<string>();
			}

			Api.AutoLogIn();

			try
			{
				string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");

				foreach (Article file in Api.GetCategoryEntries("Category:Images from the J. Willis Sayre Collection of Theatrical Photographs to check", cmtype: CMType.file))
				{
					if (File.Exists(stopFile))
					{
						File.Delete(stopFile);
						break;
					}
					if (doneFiles.Contains(file.title))
					{
						continue;
					}

					string title = file.GetTitle();
					Console.WriteLine(title);

					// check if it's a scene thing
					string fromNeedle = " from \"";
					int fromIndex = title.IndexOf(fromNeedle);
					if (fromIndex >= 0)
					{
						int closeIndex = title.IndexOf("\"", fromIndex + fromNeedle.Length);
						if (closeIndex < 0)
						{
							closeIndex = title.IndexOf("(", fromIndex + fromNeedle.Length);
						}

						int productionStart = fromIndex + fromNeedle.Length;
						string productionName = title.Substring(productionStart, closeIndex - productionStart).Trim();

						// check for the existence of that category
						Article productionCategory = Api.GetPage("Category:" + productionName);
						if (!productionCategory.missing)
						{
							// check that it's a play or film
							tree.AddToTree(productionCategory.title, 6);
							if (tree.HasParent(productionCategory.title, "Category:Films")
								|| tree.HasParent(productionCategory.title, "Category:Plays")
								|| tree.HasParent(productionCategory.title, "Category:Musicals"))
							{
								// it is!
								Article fileFull = Api.GetPage(file);
								fileFull.revisions[0].text = WikiUtils.AddCategory(productionCategory.title, fileFull.revisions[0].text);
								string uncatTemplate;
								fileFull.revisions[0].text = WikiUtils.RemoveTemplate("uncategorized", fileFull.revisions[0].text, out uncatTemplate);
								if (!string.IsNullOrEmpty(uncatTemplate))
								{
									// replace uncategorized with check categories
									fileFull.revisions[0].text = "{{subst:chc}}\n" + fileFull.revisions[0].text;
								}
								Api.EditPage(fileFull, "Adding automatically-detected production category");
								doneFiles.Add(fileFull.title);
							}
						}
					}
				}
			}
			finally
			{
				tree.Save(treeFile);
				File.WriteAllText(doneFile, JsonConvert.SerializeObject(doneFiles));
			}
		}
	}
}
