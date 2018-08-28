using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class StringUtility
{
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
