using MediaWiki;

namespace Tasks.Commons
{
	/// <summary>
	/// Refreshes the PD-Art files cache for all files in a specified category.
	/// </summary>
	public class PdArtRefreshCache : ReplaceInCategory
	{
		public PdArtRefreshCache()
			: base(new PdArtReplacement())
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false;

			Parameters["Category"] = "Category:PD-Art (PD-old default)";
			//Parameters["Category"] = "Category:PD-Art (PD-old-70)";
		}

		public override PageTitle GetCategory()
		{
			return PageTitle.Parse(Parameters["Category"]);
		}
	}
}
