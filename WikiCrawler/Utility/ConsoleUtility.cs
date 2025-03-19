using System;

public static class ConsoleUtility
{
	public static void WriteLine(ConsoleColor foregroundColor, string text, params object[] formatParams)
	{
		ConsoleColor oldColor = Console.ForegroundColor;
		Console.ForegroundColor = foregroundColor;
		Console.WriteLine(text, formatParams);
		Console.ForegroundColor = oldColor;
	}
}
