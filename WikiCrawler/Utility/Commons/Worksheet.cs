using MediaWiki;

/// <summary>
/// Base class that helps parse common info from a certain type of page.
/// </summary>
public abstract class Worksheet
{
	public readonly Article Article;
	public readonly int RevisionIndex;

	public Worksheet(Article article, int revisionIndex = 0)
	{
		Article = article;
		RevisionIndex = revisionIndex;
	}

	public string Text
	{
		get { return Article.revisions[RevisionIndex].text; }
		set
		{
			Article.revisions[RevisionIndex].text = value;
			Invalidate();
		}
	}

	public virtual void Invalidate()
	{
		
	}
}
