using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaWiki
{
	/// <summary>
	/// Contains helper methods for manipulating wikitext.
	/// </summary>
	static class WikiUtils
	{
        /// <summary>
        /// Removes the specified category from the text if it exists.
        /// </summary>
        public static string RemoveCategory(string name, string text)
		{
			//TODO: whitespace on ends is legal
			string cat = "[[" + name + "]]";
			return text.Replace(cat + "\n", "").Replace(cat, "");
		}

		public static string RemoveDuplicateCategories(string text)
		{
			HashSet<string> alreadySeen = new HashSet<string>();
			foreach (string s in GetCategories(text))
			{
				if (alreadySeen.Contains(s))
				{
					//TODO: preserve sortkeys
					text = RemoveCategory(s, text);
					text = AddCategory(s, text);
				}
				else
				{
					alreadySeen.Add(s);
				}
			}
			return text;
		}

		/// <summary>
		/// Returns true if the specified category exists in the text.
		/// </summary>
		public static bool HasCategory(string name, string text)
		{
			if (name.Length <= 0) throw new ArgumentException("Category name cannot be empty.", "name");

			if (name.StartsWith("Category:") || name.StartsWith("category:"))
			{
				name = name.Substring("Category:".Length);
			}

			foreach (string cat in GetCategories(text))
			{
				string catName = cat.Substring("Category:".Length);
				if (char.ToLower(catName[0]) == char.ToLower(name[0])
					&& catName.Substring(1) == name.Substring(1))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Add the specified category to the text.
		/// </summary>
		public static string AddCategory(string name, string text)
		{
			if (!name.StartsWith("Category:") && !name.StartsWith("category:"))
			{
				name = "Category:" + name;
			}
			if (!HasCategory(name, text))
			{
				int c = text.Length - 1;
				int lastCatStart = text.LastIndexOf("[[Category:", StringComparison.OrdinalIgnoreCase);
				if (lastCatStart >= 0)
				{
					// if there are existing categories, add after the last one
					c = text.IndexOf("]]", lastCatStart) + 1;
				}
				else
				{
					// backtrack past any interwiki links, and comments
					for (; c >= 0; c--)
					{
						if (!char.IsWhiteSpace(text[c]))
						{
							if (c > 0 && text[c] == ']' && text[c - 1] == ']')
							{
								int linkstart = text.Substring(0, c).LastIndexOf("[[");
								if (linkstart >= 0)
								{
									string content = text.Substring(linkstart + 2, c - 2 - (linkstart + 2));
									string[] contentNsSplit = content.Split(':');
									if (contentNsSplit.Length >= 2
										&& string.Compare(contentNsSplit[0], "category", true) != 0
										&& string.Compare(contentNsSplit[0], "template", true) != 0)
									{
										// interwiki, keep going
										c = linkstart;
									}
									else
									{
										// not an interwiki, break
										break;
									}
								}
								else
								{
									break;
								}
							}
							else if (c > 1 && text[c] == '>' && text[c - 1] == '-' && text[c - 2] == '-')
							{
								// backtrack through comment
								c = text.Substring(0, c).LastIndexOf("<!--");
							}
							else
							{
								break;
							}
						}
					}
				}

				return text.Substring(0, c + 1)
					+ "\n[[" + name + "]]"
					+ text.Substring(c + 1, text.Length - (c + 1));
			}
			else
			{
				return text;
			}
		}

		/// <summary>
		/// Returns true if the text contains any categories.
		/// </summary>
		public static bool HasNoCategories(string text)
		{
			return !GetCategories(text).Any();
		}

		/// <summary>
		/// Returns an array of all the directly-referenced parent categories of this article.
		/// </summary>
		public static IEnumerable<string> GetCategories(Article article)
		{
			return GetCategories(article.revisions[0].text);
		}

		/// <summary>
		/// Returns an array of all the directly-referenced parent categories of this article.
		/// </summary>
		public static IEnumerable<string> GetCategories(string text)
		{
			//TODO: whitespace after [[ is legal
			string[] catOpen = new string[] { "[[Category:", "[[category:" };
			string[] catClose = new string[] { "]]" };
			string[] sp1 = text.Split(catOpen, StringSplitOptions.None);
			for (int c = 1; c < sp1.Length; c++)
			{
				yield return "Category:" + sp1[c].Split(catClose, StringSplitOptions.None)[0].Split('|')[0].Trim();
			}
		}

		/// <summary>
		/// Remove HTML comments from the text.
		/// </summary>
		public static string RemoveComments(string text, string prefix = "<!--")
		{
			Marker start = new Marker(prefix);
			Marker end = new Marker("-->");
			int startPos = -1;
			for (int c = 0; c < text.Length; c++)
			{
				if (startPos < 0)
				{
					if (start.MatchAgainst(text[c]))
					{
						startPos = c - start.Length + 1;
					}
				}
				else
				{
					if (end.MatchAgainst(text[c]))
					{
						text = text.Substring(0, startPos) + text.Substring(c + 1);
						c = startPos - 1;
						startPos = -1;
					}
				}
			}
			return text;
		}

		public static string GetTemplateParameter(string param, string text)
		{
			int eat;
			return GetTemplateParameter(param, text, out eat);
		}

		public static string GetTemplateParameter(string param, string text, out int paramValueLocation)
		{
			int state = 0;
			Marker paramName = new Marker(param, true);
			paramValueLocation = 0;
			for (int c = 0; c < text.Length; c++)
			{
				if (state == 1)
				{
					//parameter name
					if (paramName.MatchAgainst(text[c]))
					{
						state = 2;
						continue;
					}
				}
				if (state == 2)
				{
					//equals
					if (text[c] == '=')
					{
						state = 3;
						continue;
					}
				}
				if (state == 3)
				{
					//eat whitespace
					if (!char.IsWhiteSpace(text[c]))
					{
						state = 4;
						paramValueLocation = c;
					}
				}
				if (state == 4)
				{
					//read param content
					bool templateEnd = c < text.Length - 1 && text[c] == '}' && text[c + 1] == '}';
					if (text[c] == '|' || templateEnd)
					{
						return text.Substring(paramValueLocation, c - paramValueLocation).Trim();
					}
				}

				//pipe resets any time
				if (text[c] == '|')
				{
					state = 1;
					paramName.Reset();
					continue;
				}
			}
			return "";
		}

		/// <summary>
		/// Removes the first occurence of the specified template from the text.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string RemoveTemplate(string templateName, string text)
		{
			string eat;
			return RemoveTemplate(templateName, text, out eat);
		}

		/// <summary>
		/// Removes the first occurence of the specified template from the text.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string RemoveTemplate(string templateName, string text, out string template)
		{
			//TOOD: support nested templates
			string startMarker = "{{" + templateName;
			int templateStart = text.IndexOf(startMarker);
			if (templateStart >= 0)
			{
				int templateEnd = text.IndexOf("}}", templateStart) + 2;

				// if the next character is a line return, get that too
				if (templateEnd < text.Length && text[templateEnd] == '\n')
				{
					templateEnd++;
				}

				template = text.Substring(templateStart, templateEnd - templateStart);
				return text.Substring(0, templateStart) + text.Substring(templateEnd, text.Length - templateEnd);
			}
			else
			{
				template = "";
				return text;
			}
		}

		/// <summary>
		/// Adds the specified interwiki link to the page if it doesn't already exist.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string AddInterwiki(string wiki, string page, string text)
		{
			string link = "[[" + wiki + ":" + page + "]]";
			if (!text.Contains(link))
			{
				return text + "\n" + link;
			}
			else
			{
				return text;
			}
		}

		/// <summary>
		/// Get the sortkey of the fetched article.
		/// </summary>
		public static string GetSortkey(Article article)
		{
			//TODO: support template transclusions
			if (!Article.IsNullOrEmpty(article))
			{
				string key = "{{DEFAULTSORT:";
				int defaultSort = article.revisions[0].text.IndexOf(key);
				if (defaultSort >= 0)
				{
					int defaultEnd = article.revisions[0].text.IndexOf("}}", defaultSort);
					return article.revisions[0].text.Substring(defaultSort, defaultEnd - defaultSort - key.Length - 1);
				}
				else
				{
					return article.GetTitle();
				}
			}
			else
			{
				return "";
			}
		}
	}
}
