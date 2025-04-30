using System.Collections.Generic;

public static class LinqUtility
{
	/// <summary>
	/// Returns true if the enumerable has at least X items.
	/// </summary>
	public static bool AnyX<T>(this IEnumerable<T> enumerable, int count)
	{
		int current = 0;
		foreach (T item in enumerable)
		{
			current++;
			if (current >= count)
			{
				return true;
			}
		}
		return false;
	}
}
