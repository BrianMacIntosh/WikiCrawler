using System;

public static class ExceptionUtilities
{
	public static string ToShortString(this Exception e)
	{
		string text = e.Message;
		string text2 = !string.IsNullOrEmpty(text)
			? (e.GetType().ToString() + ": " + text)
			: e.GetType().ToString();
		return text2;
	}
}
