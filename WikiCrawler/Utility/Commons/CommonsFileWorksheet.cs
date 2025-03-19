using MediaWiki;

/// <summary>
/// Simplifies access to common data that might be needed from Commons file pages.
/// </summary>
public class CommonsFileWorksheet : CommonsWorksheet
{
	/// <summary>
	/// The contents of the author, artist, etc field in a primary information template.
	/// </summary>
	public string Author
	{
		get
		{
			if (author == null)
			{
				author = GetInfoParam(authorParams, out authorParam, out authorIndex);
			}
			return author;
		}
	}

	/// <summary>
	/// The name of the parameter the 'Author' string was pulled from.
	/// </summary>
	public string AuthorParam
	{
		get
		{
			if (author == null)
			{
				author = GetInfoParam(authorParams, out authorParam, out authorIndex);
			}
			return authorParam;
		}
	}

	/// <summary>
	/// The index of the start of the author parameter.
	/// </summary>
	public int AuthorIndex
	{
		get
		{
			if (author == null)
			{
				author = GetInfoParam(authorParams, out authorParam, out authorIndex);
			}
			return authorIndex;
		}
	}

	private string author;
	private string authorParam; // name of the param the author string was actually pulled from
	private int authorIndex = -1;
	private static string[] authorParams = new string[] { "author", "artist", "photographer", "artist_display_name" };

	/// <summary>
	/// The contents of the Wikidata field in a primary information template.
	/// </summary>
	public string Wikidata
	{
		get
		{
			if (wikidata == null)
			{
				wikidata = GetInfoParam(wikidataParams, out wikidataParam, out wikidataIndex);
			}
			return wikidata;
		}
	}

	private string wikidata;
	private string wikidataParam; // name of the param the wikidata string was actually pulled from
	private int wikidataIndex = -1;
	private static string[] wikidataParams = new string[] { "wikidata" };

	/// <summary>
	/// The contents of the date field in a primary information template.
	/// </summary>
	public string Date
	{
		get
		{
			if (date == null)
			{
				date = GetInfoParam(dateParams, out dateParam, out dateIndex);
			}
			return date;
		}
		set
		{
			Text = Text.Substring(0, dateIndex) + value + Text.Substring(dateIndex + date.Length);
		}
	}

	/// <summary>
	/// The index of the start of the date parameter.
	/// </summary>
	public int DateIndex
	{
		get
		{
			if (date == null)
			{
				date = GetInfoParam(dateParams, out dateParam, out dateIndex);
			}
			return dateIndex;
		}
	}

	private string date;
	private string dateParam; // name of the param the date string was actually pulled from
	private int dateIndex = -1;
	private static string[] dateParams = new string[] { "date" };

	public CommonsFileWorksheet(Article article)
		: base(article)
	{
	}

	public override void Invalidate()
	{
		author = null;
		authorIndex = -1;
		date = null;
		dateIndex = -1;
	}

	/// <summary>
	/// Returns the contents of the first info template param that's found.
	/// </summary>
	private string GetInfoParam(string[] paramNames, out string outParam, out int index)
	{
		StringSpan templateSpan = WikiUtils.GetPrimaryInfoTemplateLocation(Text);
		if (templateSpan.IsValid)
		{
			string templateText = Text.Substring(templateSpan);

			foreach (string paramName in paramNames)
			{
				int localIndex;
				string paramContent = WikiUtils.GetTemplateParameter(paramName, templateText, out localIndex);
				if (!string.IsNullOrEmpty(paramContent))
				{
					index = localIndex + templateSpan.start;
					outParam = paramName;
					return paramContent;
				}
			}
		}

		index = -1;
		outParam = "";
		return "";
	}
}
