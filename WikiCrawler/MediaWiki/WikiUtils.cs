using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace MediaWiki
{
	public enum WikitextNestingType
	{
		Template,
		Link
	}

	/// <summary>
	/// Contains helper methods for manipulating wikitext.
	/// </summary>
	public static class WikiUtils
	{
		/// <summary>
		/// Removes the specified category from the text if it exists.
		/// </summary>
		public static string RemoveCategory(string name, string text)
		{
			return RemoveCategory(PageTitle.Parse(name), text);
		}

        /// <summary>
        /// Removes the specified category from the text if it exists.
        /// </summary>
        public static string RemoveCategory(PageTitle name, string text)
		{
			if (name.IsEmpty) throw new ArgumentException("Category name cannot be empty.", "name");

			string regexName = Regex.Escape(name.Name);

			// if the first character is a letter, it can be upper or lowercase
			if (char.IsLetter(regexName[0]))
			{
				regexName = "[" + char.ToUpper(regexName[0]).ToString() + char.ToLower(regexName[0]).ToString() + "]" + regexName.Substring(1);
			}

			Regex regex = new Regex(@"\[\[Category:" + regexName + "\\]\\][\r\n]?");

			//TODO: whitespace on ends is legal
			text = regex.Replace(text, "");
			return text;
		}

		public static string RemoveDuplicateCategories(string text)
		{
			HashSet<PageTitle> alreadySeen = new HashSet<PageTitle>();
			foreach (PageTitle s in GetCategories(text))
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
			return HasCategory(PageTitle.Parse(name), text);
		}

		/// <summary>
		/// Returns true if the specified category exists in the text.
		/// </summary>
		public static bool HasCategory(PageTitle name, string text)
		{
			if (name.IsEmpty) throw new ArgumentException("Category name cannot be empty.", "name");

			foreach (PageTitle cat in GetCategories(text))
			{
				if (name == cat)
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
			return AddCategory(PageTitle.Parse(name), text);
		}

		/// <summary>
		/// Add the specified category to the text.
		/// </summary>
		public static string AddCategory(PageTitle name, string text)
		{
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
		public static IEnumerable<PageTitle> GetCategories(Article article)
		{
			return GetCategories(article.revisions[0].text);
		}

		/// <summary>
		/// Returns an array of all the directly-referenced parent categories of this article.
		/// </summary>
		public static IEnumerable<PageTitle> GetCategories(string text)
		{
			//TODO: CHECK ME
			//TODO: whitespace after [[ is legal
			string[] catOpen = new string[] { "[[Category:", "[[category:" };
			string[] catClose = new string[] { "]]" };
			string[] sp1 = text.Split(catOpen, StringSplitOptions.None);
			for (int c = 1; c < sp1.Length; c++)
			{
				yield return new PageTitle("Category", sp1[c].Split(catClose, StringSplitOptions.None)[0].Split('|')[0].Trim());
			}
		}

		/// <summary>
		/// Remove HTML comments from the text.
		/// </summary>
		public static string RemoveComments(string text, string commentStart = "<!--")
		{
			string commentEnd = "-->";
			int startPos = text.IndexOf(commentStart);
			while (startPos > 0)
			{
				int endPos = text.IndexOf(commentEnd, startPos);
				if (endPos >= 0)
				{
					text = text.Substring(0, startPos) + text.Substring(endPos + commentEnd.Length);
					startPos = text.IndexOf(commentStart);
				}
				else
				{
					break;
				}
			}
			return text;
		}

		private static string s_templateStart = "{{";
		private static string s_templateEnd = "}}";

		private static string s_wikilinkStart = "[[";
		private static string s_wikilinkEnd = "]]";

		/// <summary>
		/// Returns the indices of the start and end of the first instance of the specified template, including {{ and }}.
		/// </summary>
		/// <param name="text">The page text to search.</param>
		/// <param name="templateName">The name of the template to search for.</param>
		/// <param name="startAt">Optionally, the index in the <paramref name="text"/> to start searching at.</param>
		/// <returns>The indices of the template, from the first { to the last }.</returns>
		public static StringSpan GetTemplateLocation(string text, string templateName, int startAt = 0)
		{
			int startIndex = GetTemplateStart(text, templateName, startAt);
			if (startIndex >= 0)
			{
				int endIndex = GetTemplateEnd(text, startIndex);
				if (endIndex >= 0)
				{
					return new StringSpan(startIndex, endIndex);
				}
			}
			
			return StringSpan.Empty;
		}

		/// <summary>
		/// Returns the text of the first instance of the specified template, including {{}}.
		/// </summary>
		public static string ExtractTemplate(string text, string templateName, int startAt = 0)
		{
			StringSpan span = GetTemplateLocation(text, templateName, startAt);
			if (span.IsValid)
			{
				return text.Substring(span);
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Returns the index of the {{ at the start of the first instance of the specified template.
		/// </summary>
		public static int GetTemplateStart(string text, string templateName, int startAt = 0)
		{
			//TODO: proper case-sensitivity
			Regex regex = new Regex(@"{{\s*:?\s*(?:Template:)?\s*" + Regex.Escape(templateName) + @"\s*[\|}]", RegexOptions.IgnoreCase);
			foreach (Match match in regex.Matches(text, startAt))
			{
				if (match.Success
					&& !IsInNowiki(text, match.Index)
					&& !IsInComment(text, match.Index))
				{
					return match.Index;
				}
			}
			return -1;
		}

		/// <summary>
		/// Returns true if the specified index in the text is inside a nowiki region.
		/// </summary>
		/// <remarks>Does not consider any part of the tags themselves inside.</remarks>
		public static bool IsInNowiki(string text, int index)
		{
			int openTags = 0;
			for (int i = 0; i < Math.Min(index, text.Length); ++i)
			{
				if (text.MatchAt("<nowiki>", i, true))
				{
					openTags++;
				}
				else if (text.MatchAt("</nowiki>", i, true))
				{
					openTags = Math.Max(0, openTags - 1);
				}
			}

			return openTags > 0;
		}

		/// <summary>
		/// Returns true if the specified index in the text is inside a commented region.
		/// </summary>
		/// <remarks>Does not consider any part of the tags themselves inside.</remarks>
		public static bool IsInComment(string text, int index)
		{
			int openTags = 0;
			for (int i = 0; i < Math.Min(index, text.Length); ++i)
			{
				if (text.MatchAt("<!--", i, true))
				{
					openTags++;
				}
				else if (text.MatchAt("-->", i, true))
				{
					openTags = Math.Max(0, openTags - 1);
				}
			}

			return openTags > 0;
		}

		public static readonly string[] PrimaryInfoTemplates = new string[]
		{
			"information",
			"Information",
			"artwork",
			"Artwork",
			"Art photo",
			"Art Photo",
			"art photo",
			"art Photo",
			"book",
			"Book",
			"photograph",
			"Photograph",
			"google Art Project",
			"Google Art Project"
		};

		public static readonly string[] ArtworkTemplates = new string[]
		{
			"artwork",
			"Artwork"
		};

		/// <summary>
		/// Returns the name of the primary info template in the page.
		/// </summary>
		public static string GetPrimaryInfoTemplate(string text)
		{
			int earliestStartIndex = int.MaxValue;
			string earliestTemplate = "";
			foreach (string template in PrimaryInfoTemplates)
			{
				int startIndex = GetTemplateStart(text, template);
				if (startIndex >= 0 && startIndex < earliestStartIndex)
				{
					earliestTemplate = template;
					earliestStartIndex = startIndex;
				}
			}
			return earliestTemplate;
		}

		/// <summary>
		/// Returns the indices of the start and end of the primary Information-like template, including {{ and }}.
		/// </summary>
		/// <param name="text">The page text to search.</param>
		public static StringSpan GetPrimaryInfoTemplateLocation(string text)
		{
			int startIndex = GetPrimaryInfoTemplateStart(text);
			if (startIndex >= 0)
			{
				int endIndex = GetTemplateEnd(text, startIndex);
				if (endIndex >= 0)
				{
					return new StringSpan(startIndex, endIndex);
				}
			}
			return StringSpan.Empty;
		}

		/// <summary>
		/// Returns the index of the {{ at the start of the primary info template;
		/// </summary>
		public static int GetPrimaryInfoTemplateStart(string text)
		{
			int? earliestStartIndex = null;
			foreach (string template in PrimaryInfoTemplates)
			{
				int startIndex = GetTemplateStart(text, template);
				if (startIndex >= 0)
				{
					if (startIndex < earliestStartIndex || !earliestStartIndex.HasValue)
					{
						earliestStartIndex = startIndex;
					}
				}
			}
			return earliestStartIndex.HasValue ? earliestStartIndex.Value : -1;
		}

		/// <summary>
		/// Returns the index of the last } at the end of this template.
		/// </summary>
		/// <param name="templateStart">The index of the {{ at the start of the template.</param>
		public static int GetTemplateEnd(string text, int templateStart)
		{
			if (templateStart < 0)
			{
				return -1;
			}

			int templatesOpen = 0;
			for (int index = templateStart; index < text.Length;)
			{
				if (text.MatchAt(s_templateStart, index))
				{
					templatesOpen++;
					index += s_templateStart.Length;
				}
				else if (text.MatchAt(s_templateEnd, index))
				{
					templatesOpen--;
					if (templatesOpen == 0)
					{
						return index -1 + s_templateEnd.Length;
					}
					else if (templatesOpen < 0)
					{
						throw new Exception();
					}
					index += s_templateEnd.Length;
				}
				else
				{
					index++;
				}
			}
			return -1;
		}

		private static readonly Regex s_templateTrimRegex = new Regex(@"^\s*{{\s*(.+)\s*}}\s*$");

		/// <summary>
		/// Trims {{}} and whitespace from the start and end of the string.
		/// </summary>
		public static string TrimTemplate(string template)
		{
			if (string.IsNullOrEmpty(template))
			{
				return template;
			}
			else if (s_templateTrimRegex.MatchOut(template, out Match match))
			{
				return match.Groups[1].Value;
			}
			else
			{
				return template;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">The content of the template (with or without {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(int param, string text)
		{
			int eat;
			return GetTemplateParameter(param, text, out eat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">The content of the template (with or without {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(int param, string text, out int paramValueLocation)
		{
			// check if this is a whole template
			bool bWholeTemplate = text.StartsWith("{{");
			int rootStackHeight = bWholeTemplate ? 1 : 0;

			// check for explicit numbered param
			string explicitParam = GetTemplateParameter(param.ToString(), text, out paramValueLocation);
			if (paramValueLocation >= 0)
			{
				return explicitParam;
			}

			// find appropriate anonymous param
			int paramNumber = 0;
			int paramStartIndex = bWholeTemplate ? 2 : 0;
			int nestedTemplates = 0;
			paramValueLocation = -1;
			for (int c = 0; c < text.Length; c++)
			{
				if (paramStartIndex >= 0)
				{
					// checking for named parameters
					if (nestedTemplates == rootStackHeight && text[c] == '=')
					{
						// presence of a named parameter excludes anonymous parameters (TODO: DOES IT?)
						return "";
					}
				}

				// template nesting
				if (text.MatchAt(s_templateStart, c))
				{
					nestedTemplates++;
					c++;
					continue;
				}
				else if (text.MatchAt(s_templateEnd, c))
				{
					nestedTemplates--;
					c++;
					continue;
				}

				// pipe resets any time
				if (nestedTemplates == rootStackHeight && text[c] == '|')
				{
					if (paramNumber == param)
					{
						paramValueLocation = paramStartIndex;
						return text.SubstringRange(paramStartIndex, c - 1);
					}
					paramNumber++;
					paramStartIndex = c + 1;
					continue;
				}
			}

			if (paramNumber == param)
			{
				paramValueLocation = paramStartIndex;
				string paramText = text.SubstringRange(paramStartIndex, text.Length - 1);
				if (bWholeTemplate && paramText.EndsWith(s_templateEnd))
				{
					return paramText.Substring(0, paramText.Length - s_templateEnd.Length).Trim();
				}
				else
				{
					return paramText;
				}
			}

			return "";
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">The content of the template (with or without {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(string param, string text)
		{
			int eat;
			return GetTemplateParameter(param, text, out eat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">The content of the template (with or without {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(string param, string text, out int paramValueLocation)
		{
			// check if this is a whole template
			bool bWholeTemplate = text.StartsWith("{{");
			int rootStackHeight = bWholeTemplate ? 1 : 0;

			int state = 0;
			Stack<WikitextNestingType> nestingStack = new Stack<WikitextNestingType>();
			paramValueLocation = -1;
			for (int c = 0; c < text.Length; )
			{
				if (state == 1)
				{
					//parameter name
					if (nestingStack.Count == rootStackHeight)
					{
						if (text.MatchAt(param, c, true))
						{
							state = 2;
							c += param.Length;
							continue;
						}
						else if (!char.IsWhiteSpace(text[c]))
						{
							// non-matching param name, reset
							state = 0;
						}
					}
				}
				else if (state == 2)
				{
					//equals
					if (text[c] == '=')
					{
						state = 3;
						c++;
						continue;
					}
				}
				else if (state == 3)
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
					bool templateEnd = c >= text.Length - 1;

					bool paramEnd = nestingStack.Count == rootStackHeight && (text[c] == '|' || text[c] == '}');
					if (paramEnd)
					{
						// do not include any trailing line returns
						for (int d = c - 1; d >= 0; d--)
						{
							if (text[d] != '\n')
							{
								c = d + 1;
								paramValueLocation = Math.Min(paramValueLocation, c);
								break;
							}
						}
					}

					if (paramEnd || templateEnd)
					{
						return text.Substring(paramValueLocation, c - paramValueLocation).Trim();
					}
				}

				//pipe resets any time
				if (nestingStack.Count == rootStackHeight && text[c] == '|')
				{
					state = 1;
					c++;
					continue;
				}

				// template nesting
				if (text.MatchAt(s_templateStart, c))
				{
					nestingStack.Push(WikitextNestingType.Template);
					c += s_templateStart.Length;
					continue;
				}
				else if (text.MatchAt(s_templateEnd, c))
				{
					if (nestingStack.Count == 0)
					{
						// unmatched close; failed to parse
						return "";
					}
					WikitextNestingType lastNest = nestingStack.Pop();
					if (lastNest != WikitextNestingType.Template)
					{
						// mismatched open/close; failed to parse
						return "";
					}
					c += s_templateEnd.Length;
					continue;
				}

				// link nesting
				if (text.MatchAt(s_wikilinkStart, c))
				{
					nestingStack.Push(WikitextNestingType.Link);
					c += s_wikilinkStart.Length;
					continue;
				}
				else if (text.MatchAt(s_wikilinkEnd, c))
				{
					if (nestingStack.Count == 0)
					{
						// unmatched close; failed to parse
						return "";
					}
					WikitextNestingType lastNest = nestingStack.Pop();
					if (lastNest != WikitextNestingType.Link)
					{
						// mismatched open/close; failed to parse
						return "";
					}
					c += s_wikilinkEnd.Length;
					continue;
				}

				c++;
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
			StringSpan span = GetTemplateLocation(text, templateName);
			if (span.IsValid)
			{
				return RemoveTemplate(text, span, out template);
			}
			else
			{
				template = "";
				return text;
			}
		}

		/// <summary>
		/// Removes the template at the specified span from the text. ASSUMES it is a template.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string RemoveTemplate(string text, StringSpan span)
		{
			return RemoveTemplate(text, span, out string template);
		}

		private static bool IsLineReturn(char c)
		{
			return c == '\n' || c == '\r';
		}

		/// <summary>
		/// Removes the template at the specified span from the text. ASSUMES it is a template.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string RemoveTemplate(string text, StringSpan span, out string template)
		{
			if (!span.IsValid)
			{
				throw new ArgumentException("'span' must be valid.");
			}

			// if the next character is a line return, get that too
			if (span.end + 1 < text.Length && IsLineReturn(text[span.end + 1]))
			{
				// ...unless it's in a parameter list
				int readIndex = span.end + 2;
				while (readIndex < text.Length && char.IsWhiteSpace(text[readIndex]))
				{
					readIndex++;
				}
				if (readIndex >= text.Length || text[readIndex] != '|')
				{
					while (IsLineReturn(text[span.end + 1]))
					{
						span.end++;
					}
				}
			}

			template = text.Substring(span);

			if (span.end == text.Length - 1)
			{
				return text.Substring(0, span.start);
			}
			else
			{
				return text.Substring(0, span.start) + text.Substring(span.end + 1);
			}
		}

		/// <summary>
		/// Returns true if the specified page text contains the specified template.
		/// </summary>\
		public static bool HasTemplate(string text, string template)
		{
			return GetTemplateLocation(text, template).IsValid;
		}

		/// <summary>
		/// Adds a Check Categories template to the specified page text.
		/// </summary>
		public static string AddCheckCategories(string text)
		{
			if (!HasTemplate(text, "Check categories"))
			{
				string uncatTemplate;
				text = RemoveTemplate("uncategorized", text, out uncatTemplate);
				text = "{{subst:chc}}\n" + text;
			}
			return text;
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
				//HACK: relies on ExtractTemplate allowing prefixes
				string defaultSort = TrimTemplate(ExtractTemplate(article.revisions[0].text, "DEFAULTSORT:"));
				if (!string.IsNullOrEmpty(defaultSort))
				{
					return defaultSort.Substring("DEFAULTSORT:".Length);
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

		/// <summary>
		/// If the page title starts with "Category:", removes it.
		/// </summary>
		public static string GetCategoryName(string category)
		{
			if (category.StartsWith("Category:", StringComparison.CurrentCultureIgnoreCase))
			{
				return category.Substring("Category:".Length);
			}
			else
			{
				return category;
			}
		}

		/// <summary>
		/// If the page title doesn't start with "Category:", prepends it.
		/// </summary>
		public static string GetCategoryCategory(string category)
		{
			if (!category.StartsWith("Category:", StringComparison.CurrentCultureIgnoreCase))
			{
				return "Category:" + category;
			}
			else
			{
				return category;
			}
		}
	}
}
