using MediaWiki;
using System;
using System.Collections.Generic;

namespace Tasks
{
	/// <summary>
	/// Performs a null edit on each file in a specified category.
	/// </summary>
	public class NullEditCategory : BaseTask
	{
		public NullEditCategory()
		{
			Parameters["Category"] = "Category:Null";
		}

		public override void Execute()
		{
			int maxEdits = int.MaxValue;

			IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries(Parameters["Category"], CMType.file);
			foreach (Article file in GlobalAPIs.Commons.FetchArticles(allFiles))
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
		}
	}
}
