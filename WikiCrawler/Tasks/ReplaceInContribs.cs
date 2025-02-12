using MediaWiki;
using System.Collections.Generic;
using System.Linq;

namespace Tasks
{
	public class ReplaceInContribs : ReplaceIn
	{
		public readonly string User;

		public ReplaceInContribs(string user, BaseReplacement replacement)
			: base(replacement)
		{
			User = user;
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			IEnumerable<Contribution> allContribs = GlobalAPIs.Commons.GetContributions(User, "now", "2025-01-25T00:00:00Z");
			while (true)
			{
				IEnumerable<Contribution> theseContribs = allContribs.Take(50);

				if (!theseContribs.Any())
				{
					break;
				}

				Article[] filesGot = GlobalAPIs.Commons.GetPages(theseContribs.Select(c => c.title).ToList(), prop: "info|revisions");

				foreach (Article file in filesGot)
				{
					yield return file;
				}

				allContribs = allContribs.Skip(50);
			}
		}
	}
}
