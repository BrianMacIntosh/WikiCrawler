﻿using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tasks
{
	public static class FixImplicitCreators
	{
		private const int s_TestLimit = int.MaxValue;
		private const int s_SearchDepth = 1;

		private static Dictionary<string, string> s_CategoriesToCreators = new Dictionary<string, string>();

		//Category:Artwork template with implicit creator
		//Category:Author matching Creator template, Creator template not used
		//Category:Book template with implicit creator

		[BatchTask]
		public static void Do()
		{
			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org"));

			Console.WriteLine("Logging in...");
			commonsApi.AutoLogIn();

			int successLimit = s_TestLimit;

			string lastPage = "";

			if (File.Exists("implicitcreators/stored.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("implicitcreators/stored.txt", FileMode.Open), Encoding.Default))
				{
					lastPage = reader.ReadLine();
					while (!reader.EndOfStream)
					{
						string[] line = reader.ReadLine().Split('|');
						s_CategoriesToCreators[line[0]] = line[1];
					}
				}
			}

			try
			{
				IEnumerable<Article> pages;
				pages = commonsApi.GetCategoryEntries("Category:Author matching Creator template, Creator template not used", cmstartsortkeyprefix: lastPage);
				foreach (Article article in pages)
				{
					Console.WriteLine("Checking '" + article.title + "'...");

					Article articleContent = commonsApi.GetPage(article);
					Do(commonsApi, articleContent);

					if (articleContent.Dirty)
					{
						CommonsCreatorFromWikidata.FixInformationTemplates(articleContent);
						commonsApi.EditPage(articleContent, articleContent.GetEditSummary());
						lastPage = WikiUtils.GetSortkey(article);
						successLimit--;
						if (successLimit <= 0) break;
					}
					else
					{
						lastPage = WikiUtils.GetSortkey(article);
					}
				}
			}
			finally
			{
				using (StreamWriter writer = new StreamWriter(new FileStream("implicitcreators/stored.txt", FileMode.Create), Encoding.Default))
				{
					writer.WriteLine(lastPage);
					foreach (KeyValuePair<string, string> kv in s_CategoriesToCreators)
					{
						writer.WriteLine(kv.Key + "|" + kv.Value);
					}
				}
			}
		}

		/// <summary>
		/// Replace any verifiable implicit creators with creator templates.
		/// </summary>
		/// <param name="article">Article, already downloaded.</param>
		/// <param name="creator">A creator that we have already determined should be used.</param>
		public static void Do(Api commonsApi, Article article, string creator = null)
		{
			string text = article.revisions[0].text;

			Console.WriteLine("FixImplictCreators: checking '" + article.title + "'.");

			if (creator != null && creator.StartsWith("Creator:"))
			{
				creator = creator.Substring("Creator:".Length);
			}

			// find author
			int authorLoc;
			string author = MediaWiki.WikiUtils.GetTemplateParameter("artist", text, out authorLoc);
			if (string.IsNullOrEmpty(author))
			{
				author = MediaWiki.WikiUtils.GetTemplateParameter("artist_display_name", text, out authorLoc);
			}
			if (string.IsNullOrEmpty(author))
			{
				author = MediaWiki.WikiUtils.GetTemplateParameter("author", text, out authorLoc);
			}
			if (string.IsNullOrEmpty(author))
			{
				author = MediaWiki.WikiUtils.GetTemplateParameter("photographer", text, out authorLoc);
			}

			if (!string.IsNullOrEmpty(author))
			{
				string replaceTemplate = "";

				// check for "anonymous" and "unknown"
				if (string.Compare("unknown", author, true) == 0)
				{
					replaceTemplate = "{{unknown|author}}";
				}
				else if (string.Compare("anonymous", author, true) == 0)
				{
					replaceTemplate = "{{anonymous}}";
				}
				else if (!string.IsNullOrEmpty(creator))
				{
					//TODO: more parsing (dob-dod, etc)
					if (author == creator)
					{
						replaceTemplate = "{{Creator:" + creator + "}}";
					}
				}
				else
				{
					foreach (string category in MediaWiki.WikiUtils.GetCategories(text))
					{
						//SPECIAL CASE: ONLY DO THINGS THAT ARE CAUGHT BY THE NEW LOGIC
						if (!category.Contains(" by "))
						{
							continue;
						}

						creator = GetCreatorForCategory(commonsApi, category, 1);
						if (!string.IsNullOrEmpty(creator))
						{
							if (string.Compare(creator, author, true) == 0)
							{
								replaceTemplate = "{{Creator:" + creator + "}}";
								break;
							}
						}
					}
				}

				// found it, place creator and update
				if (!string.IsNullOrEmpty(replaceTemplate))
				{
					Console.WriteLine("FixImplicitCreators: matched and inserted '" + replaceTemplate + "'.");
					text = text.Substring(0, authorLoc)
						+ replaceTemplate
						+ text.Substring(authorLoc + author.Length);
				}

				if (article.revisions[0].text != text)
				{
					article.revisions[0].text = text;
					article.Changes.Add("replace implicit creator");
					article.Dirty = true;
				}
			}
		}

		private static string[] templateEnd = new string[] { "}}" };

		private static string GetCreatorForCategory(Api commonsApi, string category, int currentDepth)
		{
			//force capitalize first letter
			//TODO: also force first letter of cat name
			category = char.ToUpper(category[0]) + category.Substring(1);

			if (s_CategoriesToCreators.ContainsKey(category))
			{
				return s_CategoriesToCreators[category];
			}
			else
			{
				Article categoryArticle = commonsApi.GetPage(category);
				if (!MediaWiki.Article.IsNullOrEmpty(categoryArticle))
				{
					// creator?
					int creatorLoc = categoryArticle.revisions[0].text.IndexOf("{{Creator:", StringComparison.OrdinalIgnoreCase);
					if (creatorLoc >= 0)
					{
						string creatorsub = categoryArticle.revisions[0].text.Substring(creatorLoc + "{{Creator:".Length);
						creatorsub = creatorsub.Split(templateEnd, StringSplitOptions.None)[0];
						s_CategoriesToCreators[category] = creatorsub;
						return creatorsub;
					}

					// check parent cats
					if (currentDepth < s_SearchDepth || 
						(currentDepth < s_SearchDepth + 1 && categoryArticle.GetTitle().Contains(" by ")))
					{
						foreach (string parentCat in MediaWiki.WikiUtils.GetCategories(categoryArticle.revisions[0].text))
						{
							string parentCreator = GetCreatorForCategory(commonsApi, parentCat, currentDepth + 1);
							if (!string.IsNullOrEmpty(parentCreator))
							{
								s_CategoriesToCreators[category] = parentCreator;
								return parentCreator;
							}
						}
					}
				}
			}

			return "";
		}
	}
}
