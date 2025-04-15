using System;

public static class ConsoleUtility
{
	public static void WriteLine(ConsoleColor foregroundColor, string text, params object[] formatParams)
	{
		ConsoleColor oldColor = Console.ForegroundColor;
		Console.ForegroundColor = foregroundColor;
		if (formatParams.Length > 0)
		{
			Console.WriteLine(text, formatParams);
		}
		else
		{
			Console.WriteLine(text);
		}
		Console.ForegroundColor = oldColor;
	}
}
