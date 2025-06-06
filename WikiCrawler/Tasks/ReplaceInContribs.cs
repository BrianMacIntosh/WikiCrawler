﻿using MediaWiki;
using System.Collections.Generic;
using System.Linq;

namespace Tasks
{
	/// <summary>
	/// Performs a replacement operation on files from a particular user's contributions.
	/// </summary>
	public class ReplaceInContribs : ReplaceIn
	{
		public ReplaceInContribs(string user, BaseReplacement replacement)
			: base(replacement)
		{
			Parameters["User"] = user;
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			IEnumerable<Contribution> allContribs = GlobalAPIs.Commons.GetContributions(Parameters["User"], "now", "2025-01-25T00:00:00Z");
			while (true)
			{
				IEnumerable<Contribution> theseContribs = allContribs.Take(50);

				if (!theseContribs.Any())
				{
					break;
				}

				foreach (Article file in GlobalAPIs.Commons.GetPages(theseContribs.Select(c => c.title).ToList(), prop: "info|revisions"))
				{
					yield return file;
				}

				allContribs = allContribs.Skip(50);
			}
		}
	}
}
