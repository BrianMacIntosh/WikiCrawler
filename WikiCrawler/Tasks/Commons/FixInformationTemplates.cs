using MediaWiki;
using System;
using System.Collections.Generic;

namespace Tasks
{
	public class FixInformationTemplates : BaseReplacement
	{
		private static HashSet<string> artistOrAuthor = new HashSet<string> { "artist", "author" };

		private static bool IsMainTemplate(string template)
		{
			template = template.TrimStart('{');
			foreach (string primaryTemplate in WikiUtils.PrimaryInfoTemplates)
			{
				if (string.Equals(template, primaryTemplate))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsArtTemplate(string template)
		{
			template = template.TrimStart('{');
			foreach (string primaryTemplate in WikiUtils.ArtworkTemplates)
			{
				if (string.Equals(template, primaryTemplate))
				{
					return true;
				}
			}
			return false;
		}

		public override bool DoReplacement(Article article)
		{
			return DoReplacement(article, null);
		}

		public bool DoReplacement(Article article, HashSet<string> removeEmptyParams)
		{
			if (Article.IsNullOrEmpty(article))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("  FixInformationTemplates: FATAL: article missing");
				Console.ResetColor();
				return false;
			}

			string text = article.revisions[0].text;
			bool mainTemplate = false;

			// set if either "artist" or "author" exists and is filled
			bool artTemplateHasFilledAuthor = false;

			bool nowiki = false;
			bool comment = false;
			Stack<string> openTemplates = new Stack<string>();
			int templateStartIndex = -1;
			int paramNameStartIndex = -1;
			bool hasError = false;
			for (int c = 0; c < text.Length - 1; c++)
			{
				if (text.MatchAt("<!--", c))
				{
					comment = true;
				}
				else if (text.MatchAt("-->", c))
				{
					comment = false;
				}
				if (!comment)
				{
					if (text.MatchAt("<nowiki>", c))
					{
						nowiki = true;
					}
					else if (text.MatchAt("</nowiki>", c))
					{
						nowiki = false;
					}
					if (!nowiki)
					{
						bool artTemplate;
						if (text.MatchAt("[[", c))
						{
							templateStartIndex = c;
							paramNameStartIndex = -1;
							c += 1;
						}
						else if (text.MatchAt("]]", c))
						{
							if (templateStartIndex >= 0)
							{
								templateStartIndex = -1;
							}
							else
							{
								if (openTemplates.Count == 0)
								{
									hasError = true;
								}
								else
								{
									openTemplates.Pop();
								}
								mainTemplate = openTemplates.Count > 0 && IsMainTemplate(openTemplates.Peek());
								artTemplate = openTemplates.Count > 0 && IsArtTemplate(openTemplates.Peek());
							}
							paramNameStartIndex = -1;
							c += 1;
						}
						else if (text.MatchAt("{{", c))
						{
							templateStartIndex = c;
							paramNameStartIndex = -1;
							c += 1;
						}
						else if (text.MatchAt("}}", c))
						{
							if (templateStartIndex >= 0)
							{
								templateStartIndex = -1;
							}
							else
							{
								if (openTemplates.Count == 0)
								{
									hasError = true;
								}
								else
								{
									openTemplates.Pop();
								}
								mainTemplate = openTemplates.Count > 0 && IsMainTemplate(openTemplates.Peek());
								artTemplate = openTemplates.Count > 0 && IsArtTemplate(openTemplates.Peek());
							}
							paramNameStartIndex = -1;
							c += 1;
						}
						if (templateStartIndex >= 0 && text[c] == '|')
						{
							openTemplates.Push(text.Substring(templateStartIndex, c - templateStartIndex).Trim());
							mainTemplate = openTemplates.Count > 0 && IsMainTemplate(openTemplates.Peek());
							artTemplate = openTemplates.Count > 0 && IsArtTemplate(openTemplates.Peek());
							templateStartIndex = -1;
						}
						else if (mainTemplate && text[c] == '|')
						{
							// force line returns before every main template param

							//1. backtrack through previous non-newline whitespace
							bool alreadyHasNewline = false;
							int backtrack = c - 1;
							for (; backtrack >= 0; backtrack--)
							{
								if (text[backtrack] == '\n')
								{
									alreadyHasNewline = true;
									break;
								}
								else if (!char.IsWhiteSpace(text[backtrack]))
								{
									backtrack++;
									break;
								}
							}

							//2. place a newline
							if (!alreadyHasNewline)
							{
								//TODO: instead of TrimStart, match other indentation
								text = text.Substring(0, backtrack) + "\n" + text.Substring(backtrack, text.Length - backtrack).TrimStart();
								c++;
							}

							paramNameStartIndex = c + 1;
						}

						if (text[c] == '=' && paramNameStartIndex >= 0)
						{
							string paramName = text.Substring(paramNameStartIndex, c - paramNameStartIndex).Trim();

							for (int lookahead = c + 1; lookahead < text.Length; lookahead++)
							{
								if (!char.IsWhiteSpace(text[lookahead]))
								{
									if (text[lookahead] == '|' || text.MatchAt("}}", lookahead))
									{
										//empty parameter
										if (removeEmptyParams != null && removeEmptyParams.Contains(paramName))
										{
											// remove it!
											text = text.Substring(0, paramNameStartIndex - 1) + text.Substring(lookahead, text.Length - lookahead);
											c = paramNameStartIndex - 1;
										}
									}
									else
									{
										if (paramName == "author" || paramName == "artist")
										{
											artTemplateHasFilledAuthor = true;
										}
									}
									break;
								}
							}

							paramNameStartIndex = -1;
						}
					}
				}
			}

			if (hasError)
			{
				text = WikiUtils.AddCategory("Category:Pages with mismatched parentheses", text);
			}
			else if (artTemplateHasFilledAuthor && removeEmptyParams == null)
			{
				DoReplacement(article, artistOrAuthor);
			}

			if (text != article.revisions[0].text)
			{
				article.revisions[0].text = text;
				article.Dirty = true;
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}
