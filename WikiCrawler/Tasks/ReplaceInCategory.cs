﻿using MediaWiki;
using System.Collections.Generic;

namespace Tasks
{
	/// <summary>
	/// Performs a replacement operation on all files in a particular category.
	/// </summary>
	public abstract class ReplaceInCategory : ReplaceInCategories
	{
		public ReplaceInCategory(params BaseReplacement[] replacement)
			: base(replacement)
		{

		}

		/// <summary>
		/// Returns the name of the category to affect.
		/// </summary>
		public abstract PageTitle GetCategory();

		public override sealed IEnumerable<PageTitle> GetCategories()
		{
			yield return GetCategory();
		}
	}
}
