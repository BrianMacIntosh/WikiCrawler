using MediaWiki;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Tasks.Commons
{
	public class PdArtRefreshCache : ReplaceInCategory
	{
		public PdArtRefreshCache()
			: base(new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false;
			PdArtReplacement.SkipAuthorLookup = true;
		}

		public override string GetCategory()
		{
			return "Category:PD-Art (PD-old default)";
			//return "Category:PD-Art (PD-old-70)";
		}
	}
}
