using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MediaWiki
{
	public class ParsedArticle
	{
		private ParsedArticleNode RootNode;

		public ParsedArticle(string text)
		{
			RootNode = new ParsedArticleTreeNode(text);
		}
	}

	public enum ParsedArticleNodeType
	{
		None,
		Article,
		Text,
		XmlNode,
		Wikilink,
		Template,
	}

	public abstract class ParsedArticleNode
	{

	}

	public class ParsedArticleLeafNode : ParsedArticleNode
	{
		public string Text;

		public ParsedArticleLeafNode(string text)
		{
			Text = text;
		}
	}

	public class ParsedArticleTreeNode : ParsedArticleNode
	{
		private List<ParsedArticleNode> Children = new List<ParsedArticleNode>();

		public ParsedArticleTreeNode(string text)
		{
			Marker catStart = new Marker("[[");
			Marker catEnd = new Marker("]]");
			Marker templateStart = new Marker("{{");
			Marker templateEnd = new Marker("}}");
			int catNest = 0;
			int templateNest = 0;
			for (int c = 0; c < text.Length; c++)
			{
				if (catStart.MatchAgainst(text[c]))
					catNest++;
				if (catEnd.MatchAgainst(text[c]))
					catNest--;
				if (templateStart.MatchAgainst(text[c]))
					templateNest++;
				if (templateEnd.MatchAgainst(text[c]))
					templateNest--;
			}
		}
	}
}
