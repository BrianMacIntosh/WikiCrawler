namespace Tasks
{
	/// <summary>
	/// Reports pages with malformed wikitext from a specified user's contributions.
	/// </summary>
	public class ReportMalformedContribs : ReplaceInContribs
	{
		public ReportMalformedContribs()
			: base("BMacZeroBot", new ReportMalformedPages())
		{
		}
	}
}
