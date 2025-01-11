using MediaWiki;

public class CommonsWorksheet
{
	public readonly Article Article;

	public CommonsWorksheet(Article article)
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
