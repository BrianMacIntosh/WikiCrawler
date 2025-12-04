using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class QIdTests
{
	[TestMethod]
	public void Parse()
	{
		Assert.AreEqual(new QId(1234), QId.Parse("Q1234"));
		Assert.AreEqual(new QId(1234), QId.Parse("1234"));
	}
}
