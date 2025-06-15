using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tasks.Commons;

[TestClass]
public class PdArtReplacementTests
{
	[AssemblyInitialize]
	public static void AssemblyInitialize(TestContext testContext)
	{
		PdArtReplacement.SkipCached = false;
		ImplicitCreatorsReplacement.SkipCached = false;
		ImplicitCreatorsReplacement.SlowCategoryWalk = false;
	}

	private static Article CreateArticle(string title, string content)
	{
		Article article = new Article();
		article.title = PageTitle.Parse(title);
		article.revisions = new Revision[1] { new Revision() { text = content } };
		return article;
	}

	[TestMethod]
	public void SimplePdOldDefault()
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
	public void InlineRemoveLicense()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|author={{Creator:August Macke}}
|Date=1295
|Permission={{PD-art|1=PD-1923}}
|other_versions={{Extracted from|1=AlixKypr.jpg}}
}}

=={{int:license-header}}==
{{PD-old-100}}
{{PD-1923}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|author={{Creator:August Macke}}
|Date=1295
|Permission=
|other_versions={{Extracted from|1=AlixKypr.jpg}}
}}

=={{int:license-header}}==
{{PD-Art|PD-old-auto-expired|deathyear=1914}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void NoDate()
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
	public void MalformedPdOldDefault()
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
	public void Nowiki()
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
	public void Nowiki2()
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
	public void Comment()
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
	public void Comment2()
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
	public void OtherLicenseBlocked()
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
	public void DisagreeingLicenseDeathyear()
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
{{PD-Art|deathyear=1111}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void MultiPdOldDefault()
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
	public void PdOldPdArt()
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
	public void DensePdOldDefault()
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

	[TestMethod]
	public void DensePdOldDefaultRemoval()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information|description=File description|date=1800|author={{Creator:August Macke}}|permission={{PD-Art}}|other versions=}}{{PD-Art}}
[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information|description=File description|date=1800|author={{Creator:August Macke}}|permission=|other versions=}}{{PD-Art|PD-old-auto-expired|deathyear=1914}}
[[Category:Test Category]]",
			article.revisions[0].text);
	}

	//[TestMethod]
	public void LicensedPdArt()
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

	[TestMethod]
	public void AlreadyReplaced()
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
{{PD-Art|PD-old-auto-expired|deathyear=1914}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
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
	public void AlreadyReplacedExtras()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission={{PD-1923}}
|other versions=
}}

=={{int:license-header}}==
{{PD-Art|PD-old-auto-expired|deathyear=1914}}

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
	public void SkipCreatorOption()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke|worshop of}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}

	[TestMethod]
	public void MultipleInlineCreator()
	{
		PdArtReplacement replacement = new PdArtReplacement();
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator|Wikidata=Q3504241}}
{{Creator|Wikidata=Q4260905}}
|permission=
|other versions=
}}

=={{int:license-header}}==
{{PD-Art}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
	}
}
