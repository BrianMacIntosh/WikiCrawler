using Tasks;

namespace Tasks
{
	public class ReportMalformedContribs : ReplaceInContribs
	{
		public ReportMalformedContribs()
			: base("BMacZeroBot", new ReportMalformedPages())
		{
		}
	}
}
