using MediaWiki;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tasks
{
	public class NullEditCategory
	{
		/// <summary>
		/// Performs a null edit on each file in a specified category.
		/// </summary>
		[BatchTask]
		public static void Do(string categoryName)
		{
			categoryName = WikiUtils.GetCategoryCategory(categoryName);

			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org"));
			Console.WriteLine("Logging in...");
			commonsApi.AutoLogIn();

			int maxEdits = int.MaxValue;

			IEnumerable<Article> allFiles = commonsApi.GetCategoryEntries(categoryName, CMType.file);
			while (true)
			{
				IEnumerable<Article> theseFiles = allFiles.Take(50);

				if (!theseFiles.Any())
				{
					break;
				}

				foreach (Article file in allFiles)
				{
					if (maxEdits <= 0)
					{
						break;
					}

					Console.WriteLine(file.title);

					Article fileGot = commonsApi.GetPage(file, prop: "info|revisions", iiprop: "url");
					try
					{
						commonsApi.SetPage(fileGot, "null edit");
					}
					catch (WikimediaCodeException e)
					{
						if (e.Code != "protectedpage")
						{
							throw;
						}
					}

					maxEdits--;
				}

				allFiles = allFiles.Skip(50);
			}
		}
	}
}
