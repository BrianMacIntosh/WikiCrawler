using MediaWiki;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UWash
{
	public static class SayreCategorize
	{
		public static void ProduceSayreCategories(Api commonsApi,
			CategoryTree catTree,
			Dictionary<string, string> metadata, HashSet<PageTitle> categories)
		{
			//TODO: only do this for sayre uploads?

			string title = metadata["Title"];

			// check if it's a photo of a scene from a production
			string fromNeedle = " from \"";
			int fromIndex = title.IndexOf(fromNeedle);
			if (fromIndex >= 0)
			{
				int closeIndex = title.IndexOf("\"", fromIndex + fromNeedle.Length);
				if (closeIndex < 0)
				{
					closeIndex = title.Length;
				}

				int productionStart = fromIndex + fromNeedle.Length;
				string productionName = title.Substring(productionStart, closeIndex - productionStart).Trim().Trim(',');

				// check for the existence of that category
				Article productionCategory = commonsApi.GetPage(new PageTitle(PageTitle.NS_Category, productionName));
				if (!productionCategory.missing)
				{
					// check that it's a play or film
					catTree.AddToTree(productionCategory.title, 6);
					if (catTree.HasParent(productionCategory.title, "Category:Films")
						|| catTree.HasParent(productionCategory.title, "Category:Plays")
						|| catTree.HasParent(productionCategory.title, "Category:Musicals"))
					{
						// it is!
						categories.Add(productionCategory.title);
					}
				}
			}

			// check if it's a photo of a person
			List<string> maybeNames = new List<string>();
			
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
			string[] wordSplit = title.Split(" ", StringSplitOptions.RemoveEmptyEntries);
			if (wordSplit.Length >= 2)
			{
				maybeNames.Add(wordSplit[wordSplit.Length - 2].Trim('.', ' ') + " " + wordSplit[wordSplit.Length - 1].Trim('.', ' '));
			}

			foreach (string name in maybeNames)
			{
				string nametrim = name.Trim().TrimStart("Actress ").TrimStart("Actor ");
				Article personCategory = commonsApi.GetPage(new PageTitle(PageTitle.NS_Category, nametrim));
				if (personCategory != null && !personCategory.missing)
				{
					// check that it's an actor/ess
					catTree.AddToTree(personCategory.title, 6);
					if (catTree.HasParent(personCategory.title, "Category:Performing artists"))
					{
						//TODO: check lifetime?

						categories.Add(personCategory.title);
					}
				}
			}
		}
	}
}
