namespace Tasks
{
	public class PdArtFixup : ReplaceInCategory
	{
		public PdArtFixup()
			: base (new CompoundReplacementTask(
				new ImplicitCreatorsReplacement(),
				new PdArtReplacement()))
		{

		}

		public override string GetCategory()
		{
			return "Category:PD-Art (PD-old default)";
		}
	}
}
