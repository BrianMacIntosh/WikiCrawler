using System;
using System.Globalization;
using System.Text.RegularExpressions;

public struct DateParseMetadata : IEquatable<DateParseMetadata>
{
	public static DateParseMetadata Unknown = new DateParseMetadata(0, 9999);

	/// <summary>
	/// If the date had an exact year, the year.
	/// </summary>
	public int PreciseYear;

	/// <summary>
	/// The latest year that could apply to this date.
	/// </summary>
	public int LatestYear;

	public DateParseMetadata(int preciseYear)
		: this(preciseYear, preciseYear)
	{

	}

	public DateParseMetadata(int preciseYear, int latestYear)
	{
		LatestYear = latestYear;
		PreciseYear = preciseYear;
	}

	public static DateParseMetadata Combine(DateParseMetadata a, DateParseMetadata b)
	{
		return new DateParseMetadata(
			a.PreciseYear == b.PreciseYear ? a.PreciseYear : 0,
			Math.Max(a.LatestYear, b.LatestYear));
	}

	public bool Equals(DateParseMetadata other)
	{
		return PreciseYear == other.PreciseYear && LatestYear == other.LatestYear;
	}

	public override bool Equals(object obj)
	{
		return obj is DateParseMetadata && Equals((DateParseMetadata)obj);
	}

	public override int GetHashCode()
	{
		var hashCode = 1050958002;
		hashCode = hashCode * -1521134295 + PreciseYear.GetHashCode();
		hashCode = hashCode * -1521134295 + LatestYear.GetHashCode();
		return hashCode;
	}

	public static bool operator==(DateParseMetadata a, DateParseMetadata b)
	{
		return a.Equals(b);
	}

	public static bool operator !=(DateParseMetadata a, DateParseMetadata b)
	{
		return !a.Equals(b);
	}
}

public static class DateUtility
{
	private static Regex s_exactDateRegex = new Regex("^[0-9][0-9][0-9][0-9]-[0-9][0-9]?-[0-9][0-9]?$");

	private static char[] s_dateSplit = new char[] { '-', '/' };

	/// <summary>
	/// Returns true if this is a date in the format YYYY-M[M]-D[D]
	/// </summary>
	public static bool IsExactDateModern(string date)
	{
		return s_exactDateRegex.IsMatch(date);
	}

	/// <summary>
	/// Returns the english name of the month with the specified index (1-based).
	/// </summary>
	public static string GetMonthName(int index)
	{
		switch (index)
		{
			case 1: return "January";
			case 2: return "February";
			case 3: return "March";
			case 4: return "April";
			case 5: return "May";
			case 6: return "June";
			case 7: return "July";
			case 8: return "August";
			case 9: return "September";
			case 10: return "October";
			case 11: return "November";
			case 12: return "December";
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	/// <summary>
	/// Parses the specified semi-natural date string into a Commons marked-up date.
	/// </summary>
	public static string ParseDate(string date)
	{
		DateParseMetadata metadata;
		return ParseDate(date, out metadata);
	}

	/// <summary>
	/// Parses the specified semi-natural date string into a Commons marked-up date.
	/// </summary>
	public static string ParseDate(string date, out DateParseMetadata parseMetadata, bool possibleEmptyDates = false)
	{
		DateTime parseResult;
		if (DateTime.TryParse(date, out parseResult))
		{
			if (possibleEmptyDates
				&& parseResult.Month == 1 && parseResult.Day == 1 && parseResult.Hour == 0 && parseResult.Minute == 0 && parseResult.Second == 0)
			{
				// don't trust it
				parseMetadata = DateParseMetadata.Unknown;
				return date;
			}

			parseMetadata = new DateParseMetadata(parseResult.Year);
			return string.Format("{0:0000}-{1:00}-{2:00}", parseResult.Year, parseResult.Month, parseResult.Day);
		}

		date = date.Trim('.');

		if (string.IsNullOrEmpty(date))
		{
			parseMetadata = DateParseMetadata.Unknown;
			return "{{unknown|date}}";
		}
		else if (date.EndsWith("~") || date.EndsWith("?"))
		{
			string dateStr = ParseDate(date.Substring(0, date.Length - 1).Trim(), out parseMetadata);
			parseMetadata.PreciseYear = 0;
			return "{{other date|ca|" + dateStr + "}}";
		}
		else if (date.StartsWith("ca.", StringComparison.InvariantCultureIgnoreCase))
		{
			int rml = "ca.".Length;
			string dateStr = ParseDate(date.Substring(rml, date.Length - rml).Trim(), out parseMetadata);
			parseMetadata.PreciseYear = 0;
			return "{{other date|ca|" + dateStr + "}}";
		}
		else if (date.StartsWith("circa", StringComparison.InvariantCultureIgnoreCase))
		{
			int rml = "circa".Length;
			string yearStr = date.Substring(rml, date.Length - rml).Trim();
			return "{{other date|ca|" + ParseDate(yearStr, out parseMetadata) + "}}";
		}
		else if (date.StartsWith("before"))
		{
			int rml = "before".Length;
			string yearStr = date.Substring(rml, date.Length - rml).Trim();
			int yearInt;
			parseMetadata = int.TryParse(yearStr, out yearInt)
				? new DateParseMetadata(yearInt)
				: DateParseMetadata.Unknown;
			return "{{other date|before|" + yearStr + "}}";
		}
		else if (date.StartsWith("voor/before"))
		{
			int rml = "voor/before".Length;
			string yearStr = date.Substring(rml, date.Length - rml).Trim();
			int yearInt;
			parseMetadata = int.TryParse(yearStr, out yearInt)
				? new DateParseMetadata(yearInt)
				: DateParseMetadata.Unknown;
			return "{{other date|before|" + yearStr + "}}";
		}
		else if (date.StartsWith("between "))
		{
			string[] components = date.Substring("between ".Length).Split(new string[] { " and ", "-" }, StringSplitOptions.None);
			if (components.Length == 2)
			{
				DateParseMetadata parseA, parseB;
				string a = ParseDate(components[0], out parseA);
				string b = ParseDate(components[1], out parseB);
				parseMetadata = DateParseMetadata.Combine(parseA, parseB);
				return "{{other date|between|" + a + "|" + b + "}}";
			}
			else
			{
				parseMetadata = DateParseMetadata.Unknown;
				return date;
			}
		}
		else
		{
			string[] dashsplit = date.Split(s_dateSplit);
			if (dashsplit.Length == 2 && dashsplit[0].Length == 4
				&& dashsplit[1].Length == 4)
			{
				DateParseMetadata parseA, parseB;
				string a = ParseDate(dashsplit[0], out parseA);
				string b = ParseDate(dashsplit[1], out parseB);
				parseMetadata = DateParseMetadata.Combine(parseA, parseB);
				return "{{other date|between|" + a + "|" + b + "}}";
			}
			else
			{
				return ParseSingleDate(date, out parseMetadata);
			}
		}
	}

	/// <summary>
	/// Parses a simple natural expression like "March 3 1992" into YYYY-MM-DD.
	/// </summary>
	public static string ParseSingleDate(string date, out DateParseMetadata parseMetadata)
	{
		//M/d/yyyy
		DateTime apiParse;
		if (DateTime.TryParseExact(date, "M/d/yyyy", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AllowWhiteSpaces, out apiParse))
		{
			parseMetadata = new DateParseMetadata(apiParse.Year);
			return apiParse.Year.ToString() + "-" + apiParse.Month.ToString() + "-" + apiParse.Day.ToString();
		}

		// yyyyMMdd
		if (date.Length == 8)
		{
			string year = date.Substring(0, 4);
			string month = date.Substring(4, 2);
			string day = date.Substring(6, 2);
			int iyear, imonth, iday;
			if (int.TryParse(year, out iyear) && int.TryParse(month, out imonth) && int.TryParse(day, out iday))
			{
				parseMetadata = new DateParseMetadata(iyear);
				return iyear.ToString() + "-" + imonth.ToString("00") + "-" + iday.ToString("00");
			}
		}

		// natural strings
		string[] dateSplit = date.Split(new char[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
		if (dateSplit.Length == 3)
		{
			int year, month, day;
			if (TryParseMonth(dateSplit[0], out month)
				&& int.TryParse(dateSplit[1], out day)
				&& int.TryParse(dateSplit[2], out year))
			{
				parseMetadata = new DateParseMetadata(year);
				return year.ToString() + "-" + month.ToString("00") + "-" + day.ToString("00");
			}
		}
		else if (dateSplit.Length == 2)
		{
			int year, month;
			if (TryParseMonth(dateSplit[0], out month)
				&& int.TryParse(dateSplit[1], out year))
			{
				parseMetadata = new DateParseMetadata(year);
				return year.ToString() + "-" + month.ToString("00");
			}
		}
		else if (dateSplit.Length == 1)
		{
			int year;
			if (int.TryParse(dateSplit[0], out year))
			{
				parseMetadata = new DateParseMetadata(year);
				return year.ToString();
			}
		}

		parseMetadata = DateParseMetadata.Unknown;
		return date;
	}

	/// <summary>
	/// Parses a month string to an index (one-based).
	/// </summary>
	private static bool TryParseMonth(string month, out int index)
	{
		switch (month.ToUpper())
		{
			case "JAN":
			case "JAN.":
			case "JANUARY":
				index = 1;
				return true;
			case "FEB":
			case "FEB.":
			case "FEBRUARY":
				index = 2;
				return true;
			case "MAR":
			case "MAR.":
			case "MARCH":
				index = 3;
				return true;
			case "APR":
			case "APR.":
			case "APRIL":
				index = 4;
				return true;
			case "MAY":
			case "MAY.":
				index = 5;
				return true;
			case "JUN":
			case "JUN.":
			case "JUNE":
				index = 6;
				return true;
			case "JUL":
			case "JUL.":
			case "JULY":
				index = 7;
				return true;
			case "AUG":
			case "AUG.":
			case "AUGUST":
				index = 8;
				return true;
			case "SEPT":
			case "SEPT.":
			case "SEP":
			case "SEP.":
			case "SEPTEMBER":
				index = 9;
				return true;
			case "OCT":
			case "OCT.":
			case "OCTOBER":
				index = 10;
				return true;
			case "NOV":
			case "NOV.":
			case "NOVEMBER":
				index = 11;
				return true;
			case "DEC":
			case "DEC.":
			case "DECEMBER":
				index = 12;
				return true;
			default:
				index = 0;
				return false;
		}
	}
}
