using System;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace MediaWiki
{
	public struct QId : IEquatable<QId>
	{
		public static readonly QId Empty = new QId();

		public int Id
		{
			get; set;
		}

		public bool IsEmpty
		{
			get { return Id == 0; }
		}

		public QId(int id)
		{
			Id = id;
		}

		private static readonly Regex QidRegex = new Regex("Q([0-9]+)", RegexOptions.IgnoreCase);

		/// <summary>
		/// Attempts to parse a string into a <see cref="QId"/>. Throws an exception on failure.
		/// </summary>
		public static QId Parse(string s)
		{
			Match match = QidRegex.Match(s);
			if (match.Success)
			{
				int id = int.Parse(match.Groups[1].Value);
				return new QId(id);
			}
			else if (int.TryParse(s, out int id))
			{
				return new QId(id);
			}
			else
			{
				throw new FormatException("Failed to parse QId.");
			}
		}

		/// <summary>
		/// Attempts to parse a string into a <see cref="QId"/>.
		/// </summary>
		/// <returns>Empty if parse failed.</returns>
		public static QId SafeParse(string s)
		{
			if (TryParse(s, out QId qid))
			{
				return qid;
			}
			else
			{
				return Empty;
			}
		}

		/// <summary>
		/// Attempts to parse a string into a <see cref="QId"/>.
		/// </summary>
		/// <returns>True on success.</returns>
		public static bool TryParse(string s, out QId qid)
		{
			Match match = QidRegex.Match(s);
			if (match.Success)
			{
				if (int.TryParse(match.Groups[1].Value, out int id))
				{
					qid = new QId(id);
					return true;
				}
				else
				{
					qid = Empty;
					return false;
				}
			}
			else if (int.TryParse(s, out int id))
			{
				qid = new QId(id);
				return true;
			}
			else
			{
				qid = Empty;
				return false;
			}
		}

		public bool Equals(QId other)
		{
			return other.Id == Id;
		}

		public static bool operator ==(QId left, QId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(QId left, QId right)
		{
			return !left.Equals(right);
		}

		public override int GetHashCode()
		{
			return Id;
		}

		public override bool Equals(object obj)
		{
			if (obj is QId)
			{
				return Equals((QId)obj);
			}
			else
			{
				return false;
			}
		}

		public override string ToString()
		{
			return IsEmpty ? "" : "Q" + Id;
		}

		public static explicit operator int?(QId qid)
		{
			return qid.IsEmpty ? (int?)null : qid.Id;
		}

		public static implicit operator bool(QId qid)
		{
			return !qid.IsEmpty;
		}
	}
}
