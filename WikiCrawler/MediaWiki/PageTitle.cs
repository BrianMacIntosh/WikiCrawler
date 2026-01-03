using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

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
		#region Built-in namespaces

		public const string NS_Main = "";
		public const int N_Main = 0;

		public const string NS_User = "User";
		public const int N_User = 2;

		public const string NS_Project = "Project";
		public const int N_Project = 4;

		public const string NS_File = "File";
		public const int N_File = 6;

		public const string NS_MediaWiki= "MediaWiki";
		public const int N_MediaWiki = 8;

		public const string NS_Template = "Template";
		public const int N_Template = 10;

		public const string NS_Help = "Help";
		public const int N_Help = 12;

		public const string NS_Category = "Category";
		public const int N_Category = 14;

		public const string NS_Special = "Special";
		public const int N_Special = -1;

		public const string NS_Media = "Media";
		public const int N_Media = -2;

		#endregion

		#region Commons namespaces

		public const string NS_Creator = "Creator";
		public const int N_Creator = 100;

		#endregion

		public static readonly Dictionary<string, int> s_namespaceMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			{ NS_Main, N_Main },
			{ NS_User, N_User },
			{ NS_Project, N_Project },
			{ NS_File, N_File },
			{ "Image", N_File },
			{ NS_MediaWiki, N_MediaWiki },
			{ NS_Template, N_Template },
			{ "T", N_Template },
			{ NS_Help, N_Help },
			{ NS_Category, N_Category },
			{ NS_Special, N_Special },
			{ NS_Media, N_Media },
			{ NS_Creator, N_Creator }
		};

		public static readonly PageTitle Empty = new PageTitle();

		[JsonIgnore]
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

		[JsonIgnore]
		public bool IsEmpty
		{
			get { return string.IsNullOrEmpty(Name); }
		}

		public static readonly char[] ForbiddenCharacters = new char[] { '#', '<', '>', '[', ']', '|', '{', '}', '_' };

		public static bool IsValidTitleCharacter(char c)
		{
			return c >= 32 && c != 127 && !ForbiddenCharacters.Contains(c);
		}

		public static bool ContainsInvalidTitleCharacter(string str)
		{
			return !str.All(c => IsValidTitleCharacter(c));
		}

		public PageTitle(string ns, string name)
		{
			Namespace = ns;

			if (ContainsInvalidTitleCharacter(name))
			{
				throw new ArgumentException($"PageTitle name '{name}' contains an invalid character.");
			}

			Name = name.Trim();
		}

		/// <summary>
		/// Constructs a page title, silently sanitizing the name segment of any invalid characters.
		/// </summary>
		public static PageTitle ConstructAndSanitize(string ns, string name)
		{
			name = name.Replace('[', '(');
			name = name.Replace(']', ')');
			name = name.Replace('{', '(');
			name = name.Replace('}', ')');
			name = name.Replace('<', '(');
			name = name.Replace('>', ')');
			name = name.Replace('#', '-');
			name = name.Replace('|', '-');
			name = name.Replace('_', '-');
			return new PageTitle(ns, name);
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
		public static PageTitle SafeParse(string s)
		{
			if (TryParse(s, out PageTitle pageTitle))
			{
				return pageTitle;
			}
			else
			{
				return Empty;
			}
		}

		/// <summary>
		/// Attempts to parse a string into a <see cref="PageTitle"/>.
		/// </summary>
		/// <returns>Empty if parse failed.</returns>
		public static bool TryParse(string s, out PageTitle pageTitle)
		{
			string[] split = s.Split(s_namespaceSplitter, 2);
			if (split.Length < 2)
			{
				pageTitle = Empty;
				return false;
			}
			if (!IsKnownNamespace(split[0]))
			{
				pageTitle = Empty;
				return false;
			}
			pageTitle = new PageTitle(split[0], split[1]);
			return true;
		}

		private static readonly char[] s_namespaceSplitter = new char[] { ':' };

		public static bool IsKnownNamespace(string ns)
		{
			return s_namespaceMap.ContainsKey(ns);
		}

		public bool IsNamespace(string ns)
		{
			return Namespace != null && s_namespaceMap[ns] == s_namespaceMap[Namespace];
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
			return s_namespaceMap[Namespace] * 13 + PageNameComparer.Instance.GetHashCode(Name);
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
			return IsEmpty ? "" : FullTitle;
		}

		public string ToStringNormalized()
		{
			if (IsEmpty)
			{
				return "";
			}
			else if (string.IsNullOrEmpty(Namespace))
			{
				return Name.Replace('_', ' ');
			}
			else
			{
				return Namespace.ToUpperFirst() + ":" + Name.Replace('_', ' ');
			}
		}
	}
}
