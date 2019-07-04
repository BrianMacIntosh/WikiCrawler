using System.Collections.Generic;

public static class HashSetUtility
{
	/// <summary>
	/// Adds each item in the specified collection to the hash set.
	/// </summary>
	public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> other)
	{
		foreach (T element in other)
		{
			set.Add(element);
		}
	}
}
