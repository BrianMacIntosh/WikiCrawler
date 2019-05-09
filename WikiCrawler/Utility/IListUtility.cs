using System;
using System.Collections.Generic;

public static class IListUtility
{
	/// <summary>
	/// Fills each slot in the specified list with the specified value.
	/// </summary>
	public static void Fill<T>(this IList<T> array, T value)
	{
		for (int i = 0; i < array.Count; i++)
		{
			array[i] = value;
		}
	}

	/// <summary>
	/// Returns true if the specified list is null or empty.
	/// </summary>
	public static bool IsNullOrEmpty<T>(IList<T> list)
	{
		return list == null || list.Count == 0;
	}

	/// <summary>
	/// Adds the specified object to the list if it isn't already present.
	/// </summary>
	/// <returns>True if the object was added.</returns>
	public static bool AddUnique<T>(this IList<T> list, T obj)
	{
		if (list.Contains(obj))
		{
			return false;
		}
		else
		{
			list.Add(obj);
			return true;
		}
	}

	/// <summary>
	/// Adds each object in the specified collection to the list if it isn't already present.
	/// </summary>
	public static void AddRangeUnique<T>(this IList<T> list, IEnumerable<T> items)
	{
		foreach (T item in items)
		{
			list.AddUnique(item);
		}
	}

	/// <summary>
	/// Wraps the specified index to keep it in the list's range.
	/// </summary>
	public static int WrapInRange<T>(this IList<T> list, int index)
	{
		//TODO: support larger negative indices
		return (index + list.Count) % list.Count;
	}

	/// <summary>
	/// Adds each item in the specified collection to the list.
	/// </summary>
	public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
	{
		foreach (T item in items)
		{
			list.Add(item);
		}
	}

	/// <summary>
	/// Removes all duplicate items from the list, leaving only the first instance of each item.
	/// </summary>
	public static void RemoveDuplicates<T>(this IList<T> list) where T : class
	{
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = list.Count - 1; j > i; j--)
			{
				if (list[j] == list[i])
				{
					list.RemoveAt(j);
				}
			}
		}
	}

	private static Random s_random = new Random();

	/// <summary>
	/// Randomly shuffles the elements in the list.
	/// </summary>
	public static void Shuffle<T>(this IList<T> list)
	{
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = s_random.Next(n + 1);
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
	}
}
