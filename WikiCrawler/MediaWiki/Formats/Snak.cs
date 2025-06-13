using System;
using System.Collections.Generic;

namespace MediaWiki
{
	public enum SnakType
	{
		/// <summary>
		/// Known value.
		/// </summary>
		Value,

		/// <summary>
		/// Unknown but extant value.
		/// </summary>
		SomeValue,

		/// <summary>
		/// No value exists.
		/// </summary>
		NoValue,

		/// <summary>
		/// Wikidata has no value. Not a real Wikidata snaktype.
		/// </summary>
		Unspecified,
	}

	public struct SnakValue<T> : IEquatable<SnakValue<T>> where T: struct, IEquatable<T>
	{
		public SnakType SnakType;

		public T Value;

		public bool HasValue
		{
			get { return SnakType == SnakType.Value; }
		}

		public bool IsUnknown
		{
			get { return SnakType == SnakType.SomeValue; }
		}

		public bool IsNone
		{
			get { return SnakType == SnakType.NoValue; }
		}

		/// <summary>
		/// If there's a value, returns it, otherwise returns null.
		/// </summary>
		public T? GetValueOrNull()
		{
			return HasValue ? Value : (T?)null;
		}

		/// <summary>
		/// If there's a value, returns it, otherwise returns the default.
		/// </summary>
		public T GetValueOrDefault()
		{
			return HasValue ? Value : default;
		}

		public override bool Equals(object obj)
		{
			return obj is SnakValue<T> value && Equals(value);
		}

		public bool Equals(SnakValue<T> other)
		{
			return SnakType == other.SnakType
				&& EqualityComparer<T>.Default.Equals(Value, other.Value);
		}

		public override int GetHashCode()
		{
			int hashCode = -926149666;
			hashCode = hashCode * -1521134295 + SnakType.GetHashCode();
			hashCode = hashCode * -1521134295 + Value.GetHashCode();
			return hashCode;
		}

		public SnakValue(T InValue)
		{
			SnakType = SnakType.Value;
			Value = InValue;
		}

		public SnakValue(SnakType InSnakType)
		{
			SnakType = InSnakType;
			Value = default(T);
		}

		public static bool operator ==(SnakValue<T> A, T B)
		{
			return A.HasValue && EqualityComparer<T>.Default.Equals(A.Value, B);
		}

		public static bool operator !=(SnakValue<T> A, T B)
		{
			return A.HasValue && EqualityComparer<T>.Default.Equals(A.Value, B);
		}

		public static bool operator ==(SnakValue<T> left, SnakValue<T> right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(SnakValue<T> left, SnakValue<T> right)
		{
			return !(left == right);
		}
	}

	public class Snak
	{
		public SnakType snaktype;
		public string property;
		public string datatype;
		public Dictionary<string, object> datavalue;

		public Snak(Dictionary<string, object> json)
		{
			snaktype = (SnakType)Enum.Parse(typeof(SnakType), (string)json["snaktype"], true);
			property = (string)json["property"];
			if (json.ContainsKey("datatype"))
				datatype = (string)json["datatype"];
			if (json.ContainsKey("datavalue"))
				datavalue = (Dictionary<string, object>)json["datavalue"];
		}

		public Snak(string value)
		{
			//HACK:
			datavalue = new Dictionary<string, object>
			{
				{ "value", value }
			};
		}

		/*public string GetSerialized()
		{
			//TODO:
		}*/

		public Entity GetValueAsEntity(Api api)
		{
			SnakValue<QId> qid = GetValueAsEntityId();
			if (qid.HasValue)
			{
				return api.GetEntity(qid.Value);
			}
			else
			{
				return null;
			}
		}

		public SnakValue<QId> GetValueAsEntityId()
		{
			if (snaktype == SnakType.Value)
			{
				Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
				return new SnakValue<QId>(new QId((int)entityValue["numeric-id"]));
			}
			else
			{
				return new SnakValue<QId>(snaktype);
			}
		}

		public string GetValueAsString()
		{
			return (string)datavalue["value"];
		}

		public string GetValueAsGender()
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			switch ((int)entityValue["numeric-id"])
			{
				case 6581072:
				case 1052281:
					return "female";
				case 6581097:
				case 2449503:
					return "male";
				default:
					return "";
			}
		}

		public DateTime GetValueAsDate()
		{
			if (datavalue == null)
			{
				return null;
			}

			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];

			int precision = (int)entityValue["precision"];
			return new DateTime((string)entityValue["time"], precision);
		}
	}
}
