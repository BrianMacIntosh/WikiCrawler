using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PageNameComparerTests
{
	[TestMethod]
	public void Tests()
	{
		Assert.IsTrue(PageNameComparer.Instance.Equals("Wikipedia", "Wikipedia"));
		Assert.IsTrue(PageNameComparer.Instance.Equals("Wikipedia", "wikipedia"));
		Assert.IsTrue(PageNameComparer.Instance.Equals("wikipedia", "Wikipedia"));
		Assert.IsTrue(PageNameComparer.Instance.Equals("en_wikipedia", "en_wikipedia"));
		Assert.IsTrue(PageNameComparer.Instance.Equals("en wikipedia", "en_wikipedia"));
		Assert.IsTrue(PageNameComparer.Instance.Equals("en_wikipedia", "en wikipedia"));

		Assert.IsFalse(PageNameComparer.Instance.Equals("wikipedia", "WikiPedia"));
		Assert.IsFalse(PageNameComparer.Instance.Equals("WikiPedia", "wikipedia"));
		Assert.IsFalse(PageNameComparer.Instance.Equals("enwikipedia", "eswikipedia"));
		Assert.IsFalse(PageNameComparer.Instance.Equals("en_wikipedia", "egg_wikipedia"));
		Assert.IsFalse(PageNameComparer.Instance.Equals("en wikipedia", "egg wikipedia"));
	}
}
