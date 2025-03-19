using MediaWiki;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TemplateLocationTests
{
	[TestMethod]
	public void GetTemplateLocation_TemplateFound_ReturnsCorrectIndices()
	{
		string text = "This is a page with a {{TemplateName}} inside it.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(22, span.start);
		Assert.AreEqual(37, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateNotFound_ReturnsNegativeIndices()
	{
		string text = "This is a page with no template.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(-1, span.start);
		Assert.AreEqual(-1, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateAtStart_ReturnsCorrectIndices()
	{
		string text = "{{TemplateName}} is at the start of the page.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(0, span.start);
		Assert.AreEqual(15, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateAtEnd_ReturnsCorrectIndices()
	{
		string text = "The page ends with {{TemplateName}}.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(19, span.start);
		Assert.AreEqual(34, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateWithExtraSpaces_ReturnsCorrectIndices()
	{
		string text = "This is a page with a {{ TemplateName }} inside it.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(22, span.start);
		Assert.AreEqual(39, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_MultipleInstances_ReturnsFirstInstance()
	{
		string text = "First {{TemplateName}} and then {{TemplateName}} again.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(6, span.start);
		Assert.AreEqual(21, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_MultipleInstances_ReturnsSecondInstance()
	{
		string text = "First {{TemplateName}} and then {{TemplateName}} again.";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName, 7);

		Assert.AreEqual(32, span.start);
		Assert.AreEqual(47, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_EmptyText_ReturnsNegativeIndices()
	{
		string text = "";
		string templateName = "TemplateName";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(-1, span.start);
		Assert.AreEqual(-1, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_SpecialCharactersInTemplateName_ReturnsCorrectIndices()
	{
		string text = "This is a page with a {{Template-Name_123}} inside it.";
		string templateName = "Template-Name_123";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(22, span.start);
		Assert.AreEqual(42, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_InformationTemplate_ReturnsCorrectIndices()
	{
		// Wikimedia Commons file page with an Information template
		string text = "{{Information |Description=Some description |Source={{own}}}}";
		string templateName = "Information";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(0, span.start);
		Assert.AreEqual(60, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateWithMissingClosingBrace_ReturnsNegativeIndices()
	{
		// Test a malformed template with a missing closing brace
		string text = "{{Information |Description=Some description |Source=PhotographerName";
		string templateName = "Information";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(-1, span.start);
		Assert.AreEqual(-1, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateInCommentBlock_SkipsTemplate()
	{
		// Test that the method does not detect templates inside comment blocks
		string text = "Some text before the comment block <!-- {{Information |Description=This should not be found}} --> some more text.";
		string templateName = "Information";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(-1, span.start);
		Assert.AreEqual(-1, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateInNowiki_SkipsTemplate()
	{
		// Test that the method does not detect templates inside comment blocks
		string text = "Some text before the nowiki <nowiki>{{Information |Description=This should not be found}}</nowiki> some more text.";
		string templateName = "Information";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(-1, span.start);
		Assert.AreEqual(-1, span.end);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateWithMultipleCurlyBraces_ReturnsCorrectIndices()
	{
		// Test template with multiple curly braces used inside the template parameters
		string text = "{{Information |Description={{A nested template}}}}";
		string templateName = "Information";

		StringSpan span = WikiUtils.GetTemplateLocation(text, templateName);

		Assert.AreEqual(0, span.start);
		Assert.AreEqual(49, span.end);
	}
}
