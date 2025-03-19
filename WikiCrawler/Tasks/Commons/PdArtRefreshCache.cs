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
			PdArtReplacement.SkipAuthorLookup = true;
		}

		public override string GetCategory()
		{
			return "Category:PD-Art (PD-old default)";
			//return "Category:PD-Art (PD-old-70)";
		}
	}
}
