using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

[TestClass]
public class CreatorUtilityTests
{
	[TestMethod]
	public void InlineCreatorRegexTests()
	{
		Match match;

		match = CreatorUtility.InlineCreatorTemplateRegex.Match("{{creator|wikidata=Q19974555}}");
		Assert.IsTrue(match.Success);
		Assert.AreEqual(match.Groups[1].Value, "Q19974555");

		match = CreatorUtility.InlineCreatorTemplateRegex.Match("{{creator|wikidata=Q19974555|option=after}}");
		Assert.IsFalse(match.Success);
	}
}
