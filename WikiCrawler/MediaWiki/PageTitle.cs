using System;
using System.Collections.Generic;

namespace MediaWiki
{
	public class PageNameComparer : IEqualityComparer<string>
	{
		public static readonly PageNameComparer Instance = new PageNameComparer();

		public bool Equals(string a, string b)
		{
			if (string.IsNullOrEmpty(b))
			{
				return string.IsNullOrEmpty(a);
			}
			else if (string.IsNullOrEmpty(a))
			{
				return string.IsNullOrEmpty(b);
			}
			else if (a.Length != b.Length)
			{
				return false;
			}
			else if (char.ToUpperInvariant(b[0]) != char.ToUpperInvariant(a[0]))
			{
				return false;
			}

			for (int i = 1; i < a.Length; i++)
			{
				if (!CharEquals(a[i], b[i]))
				{
					return false;
				}
			}

			return true;
		}

		private static bool CharEquals(char a, char b)
		{
			switch (a)
			{
				case ' ':
				case '_':
					return b == ' ' || b == '_';
				default:
					return a == b;
			}
		}

		public int GetHashCode(string obj)
		{
			//TODO: reduce allocations
			return obj.ToUpperFirst().Replace('_', ' ').GetHashCode();
		}
	}

	/// <summary>
	/// Represents the title of a Mediawiki page.
	/// </summary>
	public struct PageTitle : IEquatable<PageTitle>
	{
		public static readonly PageTitle Empty = new PageTitle();

		public string FullTitle
		{
			get { return string.IsNullOrEmpty(Namespace) ? Name : Namespace + ":" + Name; }
		}

		public string Namespace
		{
			get; set;
		}

		public string Name
		{
			get; set;
		}

		public bool IsEmpty
		{
			get { return string.IsNullOrEmpty(Name); }
		}

		public PageTitle(string ns, string name)
		{
			Namespace = ns;
			Name = name.Trim();
		}

		/// <summary>
		/// Attempts to parse a string into a <see cref="PageTitle"/>. Throws an exception on failure.
		/// </summary>
		public static PageTitle Parse(string s)
		{
			string[] split = s.Split(s_namespaceSplitter, 2);
			if (split.Length < 2)
			{
				return new PageTitle("", split[0]);
			}
			if (!IsKnownNamespace(split[0]))
			{
				throw new ArgumentException("Failed to parse PageTitle.");
			}
			return new PageTitle(split[0], split[1]);
		}

		/// <summary>
		/// Attempts to parse a string into a <see cref="PageTitle"/>.
		/// </summary>
		/// <returns>Empty if parse failed.</returns>
		public static PageTitle TryParse(string s)
		{
			string[] split = s.Split(s_namespaceSplitter, 2);
			if (split.Length < 2)
			{
				return Empty;
			}
			if (!IsKnownNamespace(split[0]))
			{
				return Empty;
			}
			return new PageTitle(split[0], split[1]);
		}

		private static readonly char[] s_namespaceSplitter = new char[] { ':' };

		public static bool IsKnownNamespace(string ns)
		{
			return string.Equals("Category", ns, StringComparison.OrdinalIgnoreCase)
				|| string.Equals("File", ns, StringComparison.OrdinalIgnoreCase)
				|| string.Equals("Creator", ns, StringComparison.OrdinalIgnoreCase)
				|| string.Equals("Template", ns, StringComparison.OrdinalIgnoreCase)
				|| string.Equals("Commons", ns, StringComparison.OrdinalIgnoreCase)
				|| string.Equals("Special", ns, StringComparison.OrdinalIgnoreCase)
				|| string.Equals("User", ns, StringComparison.OrdinalIgnoreCase);
		}

		public bool IsNamespace(string ns)
		{
			return string.Equals(Namespace, ns, StringComparison.OrdinalIgnoreCase);
		}

		public bool IsName(string name)
		{
			return PageNameComparer.Instance.Equals(Name, name);
		}

		public bool Equals(PageTitle other)
		{
			return IsNamespace(other.Namespace) && IsName(other.Name);
		}

		public static bool operator ==(PageTitle left, PageTitle right)
		{
			return left.Equals(right);
		}

		public static bool operator != (PageTitle left, PageTitle right)
		{
			return !left.Equals(right);
		}

		public override int GetHashCode()
		{
			return StringComparer.OrdinalIgnoreCase.GetHashCode(Namespace) * 13 + PageNameComparer.Instance.GetHashCode(Name);
		}

		public override bool Equals(object obj)
		{
			if (obj is PageTitle)
			{
				return Equals((PageTitle)obj);
			}
			else
			{
				return false;
			}
		}

		public override string ToString()
		{
			return FullTitle;
		}
	}
}
