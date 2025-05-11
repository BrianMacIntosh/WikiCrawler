using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tasks.Commons;

[TestClass]
public class ImplicitCreatorsReplacementTests
{
	private static Article CreateArticle(string title, string content)
	{
		Article article = new Article();
		article.title = title;
		article.revisions = new Revision[1] { new Revision() { text = content } };
		return article;
	}

	[TestMethod]
	public void SimpleAnonymous()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author=anonymous
|permission=
}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{anonymous}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void SimpleUnknown()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author=unknown
|permission=
}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{unknown|author}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void AlreadyAnonymous()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{anonymous}}
|permission=
}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{anonymous}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void AlreadyCreator()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void NonsenseCreator()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:jiqfio801h0}}
|permission=
}}

[[Category:Test Category]]");
		Assert.IsFalse(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:jiqfio801h0}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void CTemplateNamespace()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{c|Category:August Macke}}
|permission=
}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	[TestMethod]
	public void CTemplateNoNamespace()
	{
		ImplicitCreatorsReplacement replacement = new ImplicitCreatorsReplacement("FixImplicitCreators");
		Article article = CreateArticle("File:Test.jpg",
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{c|August Macke}}
|permission=
}}

[[Category:Test Category]]");
		Assert.IsTrue(replacement.DoReplacement(article));
		Assert.AreEqual(
@"=={{int:filedesc}}==
{{Information
|description=File description
|date=1800
|author={{Creator:August Macke}}
|permission=
}}

[[Category:Test Category]]",
			article.revisions[0].text);
	}

	//TODO: test TryMapNonTemplateString
}