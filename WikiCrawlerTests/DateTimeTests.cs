using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DateTimeTests
{
	[TestMethod]
	public void FromYear()
	{
		Assert.AreEqual(DateTime.FromYear(1973, 9).Data, "+1973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-1973, 9).Data, "-1973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(973, 9).Data, "+0973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-973, 9).Data, "-0973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(73, 9).Data, "+0073-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-73, 9).Data, "-0073-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(3, 9).Data, "+0003-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-3, 9).Data, "-0003-00-00-T00:00:00Z");
	}
}
