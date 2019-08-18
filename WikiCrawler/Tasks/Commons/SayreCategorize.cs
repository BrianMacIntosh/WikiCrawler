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
		/// <summary>
		/// Looks for scenes from productions and tries to categorize them by the production.
		/// </summary>
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
						string productionName = title.Substring(productionStart, closeIndex - productionStart).Trim().Trim(',');

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
								fileFull.revisions[0].text = WikiUtils.AddCheckCategories(fileFull.revisions[0].text);
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

		/// <summary>
		/// Looks for media with references to people and tries to add categories for those people.
		/// </summary>
		[BatchTask]
		public static void DoPeople()
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

					Article fileFull = null;
					List<string> maybeNames = new List<string>();

					title = title.Substring(0, title.IndexOf(" (SAYRE "));

					// pull out any names from the title
					string[] commaSplit = title.Split(new char[] { ',' }, 2);
					if (commaSplit.Length == 2)
					{
						maybeNames.AddRange(commaSplit[0].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries));
					}
					string[] inSplit = title.Split(" in ", StringSplitOptions.RemoveEmptyEntries);
					if (inSplit.Length >= 2)
					{
						maybeNames.AddRange(inSplit[0].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries));
					}
					string[] withSplit = title.Split(" with ", StringSplitOptions.RemoveEmptyEntries);
					if (withSplit.Length >= 2)
					{
						maybeNames.AddRange(withSplit[1].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries));
					}
					string[] includesSplit = title.Split(" Includes ", StringSplitOptions.RemoveEmptyEntries);
					if (includesSplit.Length >= 2)
					{
						maybeNames.AddRange(includesSplit[1].Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries));
					}
					if (title.StartsWith("Actor ") || title.StartsWith("Actress "))
					{
						maybeNames.Add(title);
					}

					foreach (string name in maybeNames)
					{
						string nametrim = name.Trim().TrimStart("Actress ").TrimStart("Actor ");
						Article personCategory = Api.GetPage("Category:" + nametrim);
						if (!personCategory.missing)
						{
							// check that it's an actor/ess
							tree.AddToTree(personCategory.title, 6);
							if (tree.HasParent(personCategory.title, "Category:Performing artists"))
							{
								//TODO: check lifetime?

								// it is!
								if (fileFull == null)
								{
									fileFull = Api.GetPage(file);
								}
								if (WikiUtils.HasCategory(personCategory.title, fileFull.revisions[0].text))
								{
									continue;
								}

								fileFull.Changes.Add("something");

								fileFull.revisions[0].text = WikiUtils.AddCategory(personCategory.title, fileFull.revisions[0].text);

								// remove overcat
								//TODO:
							}
						}
					}

					if (fileFull != null && fileFull.Changes.Count > 0)
					{
						fileFull.revisions[0].text = WikiUtils.AddCheckCategories(fileFull.revisions[0].text);

						// remove some hamfisted categorization
						fileFull.revisions[0].text = WikiUtils.RemoveCategory("Category:Actors", fileFull.revisions[0].text);
						fileFull.revisions[0].text = WikiUtils.RemoveCategory("Category:Actresses", fileFull.revisions[0].text);

						Api.EditPage(fileFull, "Adding automatically-detected person category(s)");
						doneFiles.Add(fileFull.title);
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
