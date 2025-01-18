namespace Tasks
{
	public class FixImplicitCreators : ReplaceInCategory
	{
		public FixImplicitCreators()
			: base(new CompoundReplacementTask(new ImplicitCreatorsReplacement())
				  .Conditional(new FixInformationTemplates()))
		{

		}

		public override string GetCategory()
		{
			//Category:Artwork template with implicit creator
			//Category:Author matching Creator template, Creator template not used
			//Category:Book template with implicit creator
			return "Category:Author matching Creator template, Creator template not used";
		}
	}
}
