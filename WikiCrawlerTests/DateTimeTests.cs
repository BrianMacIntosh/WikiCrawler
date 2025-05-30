using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DateTimeTests
{
	[TestMethod]
	public void FromYear()
	{
		Assert.AreEqual(DateTime.FromYear(1973, DateTime.YearPrecision).Data, "+1973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-1973, DateTime.YearPrecision).Data, "-1973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(973, DateTime.YearPrecision).Data, "+0973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-973, DateTime.YearPrecision).Data, "-0973-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(73, DateTime.YearPrecision).Data, "+0073-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-73, DateTime.YearPrecision).Data, "-0073-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(3, DateTime.YearPrecision).Data, "+0003-00-00-T00:00:00Z");
		Assert.AreEqual(DateTime.FromYear(-3, DateTime.YearPrecision).Data, "-0003-00-00-T00:00:00Z");
	}

	[TestMethod]
	public void GetLatestYear()
	{
		Assert.AreEqual(1973, DateTime.FromYear(1973, DateTime.YearPrecision).GetLatestYear());

		Assert.AreEqual(1979, DateTime.FromYear(1970, DateTime.DecadePrecision).GetLatestYear());
		Assert.AreEqual(1979, DateTime.FromYear(1979, DateTime.DecadePrecision).GetLatestYear());

		Assert.AreEqual(1899, DateTime.FromYear(1900, DateTime.CenturyPrecision).GetLatestYear());
		Assert.AreEqual(1999, DateTime.FromYear(1901, DateTime.CenturyPrecision).GetLatestYear());
		Assert.AreEqual(1999, DateTime.FromYear(1999, DateTime.CenturyPrecision).GetLatestYear());
		Assert.AreEqual(1999, DateTime.FromYear(2000, DateTime.CenturyPrecision).GetLatestYear());
	}
}
