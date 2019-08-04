using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

public static class StringUtility
{
	public static char[] Pipe = { '|' };
	public static string[] LineBreak = { "<br/>", "<br />", "<br>", "\r", "\n" };
	public static string[] RawLineBreak = { "\r\n", "\n" };
	public static string[] WhitespaceExtended = { "<br/>", "<br />", "<br>", "\r", "\n", " ", "\t" };
	public static char[] Colon = { ':' };
	public static char[] Parens = { '(', ')', ' ' };
	public static string[] DashDash = new string[] { "--" };
	public static char[] Equal = new char[] { '=' };

	public static string SubstringRange(this string str, int start, int end)
	{
		return str.Substring(start, end - start + 1);
	}

	/// <summary>
	/// Removes certain HTML tags if they are enclosing the entire string.
	/// </summary>
	public static string TrimSingleEnclosingTags(this string text)
	{
		text = TrimSoloPair(text, "<text>", "</text>");
		text = TrimSoloPair(text, "<p>", "</p>");
		text = TrimSoloPair(text, "<span>", "</span>");

		return text;
	}

	/// <summary>
	/// Removes the start and end strings from the start and end of the string if they only occur there.
	/// </summary>
	public static string TrimSoloPair(this string text, string start, string end)
	{
		if (text.StartsWith(start) && text.EndsWith(end) && text.IndexOf(start, 1) < 0)
		{
			text = text.Substring(start.Length, text.Length - start.Length - end.Length);
		}
		return text;
	}

	public static string Trim(this string text, string[] strings)
	{
		string oldText;
		do
		{
			oldText = text;
			foreach (string trim in strings)
			{
				text = text.Trim(trim);
			}
		} while (oldText != text);
		return text;
	}

	public static string Trim(this string text, string str)
	{
		text = text.TrimStart(str);
		text = text.TrimEnd(str);
		return text;
	}

	public static string TrimStart(this string text, string str)
	{
		if (text.StartsWith(str))
		{
			text = text.Substring(str.Length);
		}
		return text;
	}

	public static string TrimEnd(this string text, string str)
	{
		if (text.EndsWith(str))
		{
			text = text.Substring(0, text.Length - str.Length);
		}
		return text;
	}

	/// <summary>
	/// Removes all whitespace that starts a line.
	/// </summary>
	public static string RemoveIndentation(this string text)
	{
		string[] lines = text.Split(RawLineBreak, System.StringSplitOptions.None);
		string result = "";
		foreach (string line in lines)
		{
			result = Join("\n", result, line.TrimStart());
		}
		return result;
	}

	/// <summary>
	/// Removes all line returns more than two in a row.
	/// </summary>
	public static string RemoveExtraneousLines(this string text)
	{
		string[] lines = text.Split(RawLineBreak, System.StringSplitOptions.None);
		string result = "";
		bool lastLineBlank = true;
		foreach (string line in lines)
		{
			bool thisLineBlank = string.IsNullOrWhiteSpace(line);
			if (thisLineBlank && lastLineBlank)
			{
				continue;
			}
			result = Join("\n", result, line);
			lastLineBlank = thisLineBlank;
		}
		return result;
	}

	/// <summary>
	/// Converts the first character of the string to lower case.
	/// </summary>
	public static string ToLowerFirst(this string text)
	{
		return char.ToLower(text[0]).ToString() + text.Substring(1);
	}

	/// <summary>
	/// Converts the first character of the string to upper case.
	/// </summary>
	public static string ToUpperFirst(this string text)
	{
		return char.ToUpper(text[0]).ToString() + text.Substring(1);
	}

	/// <summary>
	/// Capitalizes the first letter of each word.
	/// </summary>
	public static string ToTitleCase(this string text)
	{
		bool lastWhitespace = true;
		StringBuilder builder = new StringBuilder();
		for (int i = 0; i < text.Length; i++)
		{
			builder.Append(lastWhitespace ? char.ToUpper(text[i]) : text[i]);
			lastWhitespace = char.IsWhiteSpace(text[i]);
		}
		return builder.ToString();
	}

	/// <summary>
	/// Removes HTML tags from the specified string.
	/// </summary>
	public static string CleanHtml(string text)
	{
		int startIndex = -1;
		for (int c = text.Length - 1; c >= 0; c--)
		{
			if (startIndex < 0)
			{
				if (text[c] == '>')
				{
					startIndex = c;
				}
			}
			else
			{
				if (text[c] == '<')
				{
					string tagContent = text.Substring(c + 1, startIndex - c - 1);
					if (!tagContent.Trim().StartsWith("br"))
					{
						text = text.Remove(c, startIndex - c + 1);
					}
					startIndex = -1;
				}
			}
		}

		text = WebUtility.HtmlDecode(text);

		text = text.Trim();

		//TODO trim brs

		return text;
	}

	public static string FormatOrdinal(int number)
	{
		int ones = number % 10;
		int tens = number % 100;
		switch (tens)
		{
			case 11:
			case 12:
			case 13:
				return number + "th";
		}
		switch (ones)
		{
			case 1:
				return number + "st";
			case 2:
				return number + "nd";
			case 3:
				return number + "rd";
		}
		return number + "th";
	}

	public static string[] Split(this string str, string splitter, System.StringSplitOptions options)
	{
		return str.Split(new string[] { splitter }, options);
	}

	public static string Join(string delimiter, string a, string b)
	{
		if (string.IsNullOrEmpty(a))
		{
			return b;
		}
		else if (string.IsNullOrEmpty(b))
		{
			return a;
		}
		else
		{
			return a + delimiter + b;
		}
	}
	
	public static string Replace(this string str, IList<string> targets, string replacement)
	{
		foreach (string target in targets)
		{
			str = str.Replace(target, replacement);
		}
		return str;
	}
}
