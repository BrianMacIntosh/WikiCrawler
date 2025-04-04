namespace Tasks.Commons
{
	public class FixImplicitCreators : ReplaceInCategory
	{
		public FixImplicitCreators()
			: base(new ImplicitCreatorsReplacement("FixImplicitCreators"), new FixInformationTemplates(), new LocalizeDateReplacement())
		{
			Parameters["Category"] = "Category:Author matching Creator template, Creator template not used";
			//Parameters["Category"] = "Category:Artwork template with implicit creator";
			//Parameters["Category"] = "Category:Author matching Creator template, Creator template not used";
			//Parameters["Category"] = "Category:Book template with implicit creator";
		}

		public override string GetCategory()
		{
			return Parameters["Category"];
		}
	}
}
