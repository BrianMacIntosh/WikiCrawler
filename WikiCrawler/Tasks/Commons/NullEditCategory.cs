using MediaWiki;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tasks
{
	/// <summary>
	/// Performs a null edit on each file in a specified category.
	/// </summary>
	public class NullEditCategory : BaseTask
	{
		public override void Execute()
		{
			Console.Write("Category>");
			string categoryName = Console.ReadLine();
			categoryName = WikiUtils.GetCategoryCategory(categoryName);

			int maxEdits = int.MaxValue;

			IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries(categoryName, CMType.file);
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

					Article fileGot = GlobalAPIs.Commons.GetPage(file, prop: "info|revisions", iiprop: "url");
					try
					{
						GlobalAPIs.Commons.SetPage(fileGot, "null edit");
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
