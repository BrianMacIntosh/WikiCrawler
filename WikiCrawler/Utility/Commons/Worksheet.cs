using MediaWiki;

/// <summary>
/// Base class that helps parse common info from a certain type of page.
/// </summary>
public abstract class Worksheet
{
	public readonly Article Article;

	public Worksheet(Article article)
	{
		Article = article;
	}

	public string Text
	{
		get { return Article.revisions[0].text; }
		set
		{
			Article.revisions[0].text = value;
			Invalidate();
		}
	}

	public virtual void Invalidate()
	{
		
	}
}
