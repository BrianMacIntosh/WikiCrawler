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

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(22, startIndex);
		Assert.AreEqual(37, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateNotFound_ReturnsNegativeIndices()
	{
		string text = "This is a page with no template.";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(-1, startIndex);
		Assert.AreEqual(-1, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateAtStart_ReturnsCorrectIndices()
	{
		string text = "{{TemplateName}} is at the start of the page.";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(0, startIndex);
		Assert.AreEqual(15, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateAtEnd_ReturnsCorrectIndices()
	{
		string text = "The page ends with {{TemplateName}}.";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(19, startIndex);
		Assert.AreEqual(34, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateWithExtraSpaces_ReturnsCorrectIndices()
	{
		string text = "This is a page with a {{ TemplateName }} inside it.";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(22, startIndex);
		Assert.AreEqual(39, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_MultipleInstances_ReturnsFirstInstance()
	{
		string text = "First {{TemplateName}} and then {{TemplateName}} again.";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(6, startIndex);
		Assert.AreEqual(21, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_MultipleInstances_ReturnsSecondInstance()
	{
		string text = "First {{TemplateName}} and then {{TemplateName}} again.";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex, 7);

		Assert.AreEqual(32, startIndex);
		Assert.AreEqual(47, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_EmptyText_ReturnsNegativeIndices()
	{
		string text = "";
		string templateName = "TemplateName";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(-1, startIndex);
		Assert.AreEqual(-1, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_SpecialCharactersInTemplateName_ReturnsCorrectIndices()
	{
		string text = "This is a page with a {{Template-Name_123}} inside it.";
		string templateName = "Template-Name_123";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(22, startIndex);
		Assert.AreEqual(42, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_InformationTemplate_ReturnsCorrectIndices()
	{
		// Wikimedia Commons file page with an Information template
		string text = "{{Information |Description=Some description |Source={{own}}}}";
		string templateName = "Information";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(0, startIndex);
		Assert.AreEqual(60, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateWithMissingClosingBrace_ReturnsNegativeIndices()
	{
		// Test a malformed template with a missing closing brace
		string text = "{{Information |Description=Some description |Source=PhotographerName";
		string templateName = "Information";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(0, startIndex);
		Assert.AreEqual(-1, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateInCommentBlock_SkipsTemplate()
	{
		// Test that the method does not detect templates inside comment blocks
		string text = "Some text before the comment block <!-- {{Information |Description=This should not be found}} --> some more text.";
		string templateName = "Information";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(-1, startIndex);
		Assert.AreEqual(-1, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateInNowiki_SkipsTemplate()
	{
		// Test that the method does not detect templates inside comment blocks
		string text = "Some text before the nowiki <nowiki>{{Information |Description=This should not be found}}</nowiki> some more text.";
		string templateName = "Information";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(-1, startIndex);
		Assert.AreEqual(-1, endIndex);
	}

	[TestMethod]
	public void GetTemplateLocation_TemplateWithMultipleCurlyBraces_ReturnsCorrectIndices()
	{
		// Test template with multiple curly braces used inside the template parameters
		string text = "{{Information |Description={{A nested template}}}}";
		string templateName = "Information";

		int startIndex, endIndex;

		WikiUtils.GetTemplateLocation(text, templateName, out startIndex, out endIndex);

		Assert.AreEqual(0, startIndex);
		Assert.AreEqual(49, endIndex);
	}
}
