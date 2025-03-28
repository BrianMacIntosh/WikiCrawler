using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tasks;

[TestClass]
public class PdArtReplacementTests
{
	[AssemblyInitialize]
	public static void AssemblyInitialize(TestContext testContext)
	{
		PdArtReplacement.SkipCached = false;
		PdArtReplacement.SkipAuthorLookup = true;
		ImplicitCreatorsReplacement.SlowCategoryWalk = false;
	}

	[AssemblyCleanup]
	public static void AssemblyCleanup()
	{
		CreatorUtilityMeta.SaveOut();
	}

	private static Article CreateArticle(string title, string content)
	{
		Article article = new Article();
		article.title = title;
		article.revisions = new Revision[1] { new Revision() { text = content } };
		return article;
	}

	[TestMethod]
	public void PdArtReplacementTests_SimplePdOldDefault()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art|PD-old-auto-expired|deathyear=1914}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void PdArtReplacementTests_NoDate()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void PdArtReplacementTests_MalformedPdOldDefault()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void PdArtReplacementTests_Nowiki()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
<nowiki>{{PD-Art}}</nowiki>

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void PdArtReplacementTests_Nowiki2()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}<nowiki>

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]</nowiki>");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void PdArtReplacementTests_Comment()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
<!--{{PD-Art}}-->

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void PdArtReplacementTests_Comment2()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}<!--

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]-->");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void PdArtReplacementTests_OtherLicenseBlocked()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}{{PD-Russia}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void PdArtReplacementTests_MultiPdOldDefault()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission={{PD-Art}}
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art|PD-old-auto-expired|deathyear=1914}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void PdArtReplacementTests_PdOldPdArt()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission={{PD-old}}
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art|PD-old-auto-expired|deathyear=1914}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void PdArtReplacementTests_DensePdOldDefault()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information|description=File description|date=1800|author={{Creator:August Macke}}|permission={{PD-Art}}|other versions=}}
[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information|description=File description|date=1800|author={{Creator:August Macke}}|permission={{PD-Art|PD-old-auto-expired|deathyear=1914}}|other versions=}}
[[Category:Test Category]]",
			article.revisions[0].text);
	}

	//[TestMethod]
	public void PdArtReplacementTests_LicensedPdArt()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission={{Licensed-PD-Art|PD-old|Cc-zero}}
|other versions=
}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission={{Licensed-PD-Art|PD-old-auto-expired|deathyear=1914|Cc-zero}}
|other versions=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}
}
