using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI;

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
		/// Returns the indices of the start and end of the inner content of the first instance of the specified template.
		/// </summary>
		public static void GetTemplateLocation(string text, string templateName, out int startIndex, out int endIndex)
		{
			startIndex = GetTemplateStart(text, templateName);
			if (startIndex >= 0)
			{
				endIndex = GetTemplateEnd(text, startIndex);
				startIndex += s_templateStart.Length;
			}
			else
			{
				endIndex = -1;
			}
		}

		/// <summary>
		/// Returns the inner contents of the first instance of the specified template.
		/// </summary>
		public static string ExtractTemplate(string text, string templateName)
		{
			GetTemplateLocation(text, templateName, out int startIndex, out int endIndex);
			if (startIndex >= 0)
			{
				return text.SubstringRange(startIndex, endIndex);
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Returns the index of the {{ at the start of the first instance of the specified template.
		/// </summary>
		public static int GetTemplateStart(string text, string templateName)
		{
			//TODO: proper case-sensitivity
			//TODO: does not check that template name ends on a boundary
			return text.IndexOf("{{" + templateName, StringComparison.InvariantCultureIgnoreCase);
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
				int startIndex = text.IndexOf("{{" + template);
				if (startIndex >= 0 && startIndex < earliestStartIndex)
				{
					earliestTemplate = template;
					earliestStartIndex = startIndex;
				}
			}
			return earliestTemplate;
		}

		/// <summary>
		/// Returns the index of the {{ at the start of the primary info template;
		/// </summary>
		public static int GetPrimaryInfoTemplateStart(string text)
		{
			int? earliestStartIndex = null;
			foreach (string template in PrimaryInfoTemplates)
			{
				int startIndex = text.IndexOf("{{" + template);
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
		/// Returns the index of the last character before the }} at the end of this template.
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
						return index - 1;
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">Only the text of the template (with no enclosing {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(int param, string text)
		{
			int eat;
			return GetTemplateParameter(param, text, out eat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">Only the text of the template (with no enclosing {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(int param, string text, out int paramValueLocation)
		{
			// check for explicit numbered param
			string explicitParam = GetTemplateParameter(param.ToString(), text, out paramValueLocation);
			if (paramValueLocation >= 0)
			{
				return explicitParam;
			}

			// find appropriate anonymous param
			int paramNumber = 0;
			int paramStartIndex = -1;
			int nestedTemplates = 0;
			paramValueLocation = -1;
			for (int c = 0; c < text.Length; c++)
			{
				if (paramStartIndex >= 0)
				{
					// checking for named parameters
					if (nestedTemplates <= 0 && text[c] == '=')
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
				if (nestedTemplates <= 0 && text[c] == '|')
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
				return text.SubstringRange(paramStartIndex, text.Length - 1);
			}

			return "";
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">Only the text of the template (with no enclosing {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(string param, string text)
		{
			int eat;
			return GetTemplateParameter(param, text, out eat);
		}

		private enum NestingType
		{
			Template,
			Link
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="text">Only the text of the template (with no enclosing {{}}).</param>
		/// <returns></returns>
		public static string GetTemplateParameter(string param, string text, out int paramValueLocation)
		{
			int state = 0;
			Stack<NestingType> nestingStack = new Stack<NestingType>();
			paramValueLocation = -1;
			for (int c = 0; c < text.Length; c++)
			{
				if (state == 1)
				{
					//parameter name
					if (nestingStack.Count <= 0 && text.MatchAt(param, c, true))
					{
						state = 2;
						c += param.Length - 1;
						continue;
					}
				}
				else if (state == 2)
				{
					//equals
					if (text[c] == '=')
					{
						state = 3;
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

				// template nesting
				if (text.MatchAt(s_templateStart, c))
				{
					nestingStack.Push(NestingType.Template);
					c++;
					continue;
				}
				else if (text.MatchAt(s_templateEnd, c))
				{
					if (nestingStack.Count == 0)
					{
						// unmatched close; failed to parse
						return "";
					}
					NestingType lastNest = nestingStack.Pop();
					if (lastNest != NestingType.Template)
					{
						// mismatched open/close; failed to parse
						return "";
					}
					c++;
					continue;
				}

				// link nesting
				if (text.MatchAt(s_wikilinkStart, c))
				{
					nestingStack.Push(NestingType.Link);
					c++;
					continue;
				}
				else if (text.MatchAt(s_wikilinkEnd, c))
				{
					if (nestingStack.Count == 0)
					{
						// unmatched close; failed to parse
						return "";
					}
					NestingType lastNest = nestingStack.Pop();
					if (lastNest != NestingType.Link)
					{
						// mismatched open/close; failed to parse
						return "";
					}
					c++;
					continue;
				}

				if (state == 4)
				{
					//read param content
					bool templateEnd = c >= text.Length - 1;

					bool paramEnd = nestingStack.Count <= 0 && text[c] == '|';
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
				if (nestingStack.Count <= 0 && text[c] == '|')
				{
					state = 1;
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
		/// Returns true if the specified page text contains the specified template.
		/// </summary>\
		public static bool HasTemplate(string text, string template)
		{
			// template names are case-insensitive on the first letter
			return text.Contains("{{" + template.ToLowerFirst()) || text.Contains("{{" + template.ToUpperFirst());
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
