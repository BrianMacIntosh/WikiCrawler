using MediaWiki;

/// <summary>
/// Simplifies access to common data that might be needed from Commons creator pages.
/// </summary>
public class CommonsCreatorWorksheet : CommonsWorksheet
{
	/// <summary>
	/// The contents of the Wikidata parameter.
	/// </summary>
	public string Wikidata
	{
		get
		{
			if (wikidata == null)
			{
				wikidata = GetInfoParam(wikidataParams, out wikidataIndex);
			}
			return wikidata;
		}
	}

	/// <summary>
	/// The index of the start of the author parameter.
	/// </summary>
	public int WikidataIndex
	{
		get
		{
			if (wikidata == null)
			{
				wikidata = GetInfoParam(wikidataParams, out wikidataIndex);
			}
			return wikidataIndex;
		}
	}

	private string wikidata;
	private int wikidataIndex = -1;
	private static string[] wikidataParams = new string[] { "wikidata" };

	public CommonsCreatorWorksheet(Article article)
		: base(article)
	{
	}

	public override void Invalidate()
	{
		wikidata = null;
		wikidataIndex = -1;
	}

	/// <summary>
	/// Returns the contents of the first Creator template param that's found.
	/// </summary>
	private string GetInfoParam(string[] paramNames, out int index)
	{
		int templateStart = WikiUtils.GetTemplateStart(Text, "Creator");
		if (templateStart >= 0)
		{
			int templateEnd = WikiUtils.GetTemplateEnd(Text, templateStart);
			string templateText = Text.SubstringRange(templateStart + 2, templateEnd);

			foreach (string paramName in paramNames)
			{
				string paramContent = WikiUtils.GetTemplateParameter(paramName, templateText, out index);
				if (!string.IsNullOrEmpty(paramContent))
				{
					return paramContent;
				}
			}

			index = -1;
			return "";
		}
		else
		{
			index = -1;
			return "";
		}
	}
}
