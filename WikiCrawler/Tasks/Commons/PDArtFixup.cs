namespace Tasks
{
	public class PdArtFixup : ReplaceInCategory
	{
		public static BaseReplacement CreateReplacement()
		{
			return new CompoundReplacementTask(new ImplicitCreatorsReplacement("PdArtReplacement"), new LocalizeDateReplacement(), new PdArtReplacement(), new FixInformationTemplates());
		}

		public PdArtFixup()
			: base(CreateReplacement())
		{

		}

		public override string GetCategory()
		{
			return "Category:PD-Art (PD-old default)";
		}
	}
}
