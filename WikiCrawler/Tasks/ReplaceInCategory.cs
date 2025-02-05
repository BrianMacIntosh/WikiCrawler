using MediaWiki;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tasks
{
	/// <summary>
	/// Performs a replacement operation on all files in a particular category.
	/// </summary>
	public abstract class ReplaceInCategory : ReplaceIn
	{
		public ReplaceInCategory(BaseReplacement replacement)
			: base(replacement)
		{

		}

		/// <summary>
		/// Returns the name of the category to affect.
		/// </summary>
		public abstract string GetCategory();

		public override IEnumerable<Article> GetFilesToAffectUncached(string startSortkey)
		{
			IEnumerable<Article> allFiles = GlobalAPIs.Commons.GetCategoryEntries(GetCategory(), CMType.file, cmstartsortkeyprefix: startSortkey);

			Article[] buffer = new Article[500];
			int bufferPtr = 0;

			// query for articles in batches of fixed size
			foreach (Article article in allFiles)
			{
				if (bufferPtr < buffer.Length)
				{
					buffer[bufferPtr++] = article;
					continue;
				}

				Article[] filesGot = GlobalAPIs.Commons.GetPages(buffer, prop: "info|revisions");
				foreach (Article file in filesGot)
				{
					yield return file;
				}

				bufferPtr = 0;
				Array.Clear(buffer, 0, buffer.Length);
			}

			if (bufferPtr > 0)
			{
				// pick up the last incomplete batch
				Array.Resize(ref buffer, bufferPtr);
				Article[] filesGot = GlobalAPIs.Commons.GetPages(buffer, prop: "info|revisions");
				foreach (Article file in filesGot)
				{
					yield return file;
				}
			}
		}
	}
}
