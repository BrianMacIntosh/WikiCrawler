using System.Collections.Generic;
using System.Net;

public static class StringUtility
{
	public static char[] Pipe = { '|' };
	public static string[] LineBreak = { "|", "<br/>", "<br />", "<br>", "\r\n", "\n" }; //TEMP: pipe is temp (napoleon only)
	public static char[] Colon = { ':' };
	public static char[] Parens = { '(', ')', ' ' };
	public static string[] DashDash = new string[] { "--" };
	public static char[] Equal = new char[] { '=' };

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
