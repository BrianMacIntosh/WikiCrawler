using System;
using System.Globalization;

public static class CommonsTemplates
{
	public static string MakeUncategorizedTemplate()
	{
		string month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
		return "{{Uncategorized|year=" + DateTime.Now.Year + "|month=" + month + "|day=" + DateTime.Now.Day + "}}";
	}
}
