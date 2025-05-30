using System;
using System.Diagnostics;

namespace MediaWiki
{
	public class DateTime
	{
		public const int MilleniumPrecision = 6;
		public const int CenturyPrecision = 7;
		public const int DecadePrecision = 8;
		public const int YearPrecision = 9;
		public const int MonthPrecision = 10;
		public const int DayPrecision = 11;
		public const int HourPrecision = 12;
		public const int MinutePrecision = 13;
		public const int SecondPrecision = 14;

		public int Precision;
		public string Data;

		public DateTime(string data, int precision)
		{
			Data = data;
			Precision = precision;
		}

		public static DateTime FromYear(int year, int precision)
		{
			return new DateTime(string.Format("{0:\\+0000;-0000}-00-00-T00:00:00Z", year), precision);
		}

		public string GetString(int maxPrecision)
		{
			int precision = Precision;

			if (precision > maxPrecision) precision = maxPrecision;

			if (precision <= MilleniumPrecision)
			{
				return Data.Substring(0, 2) + "XXX";
			}
			else if (precision <= CenturyPrecision)
			{
				return Data.Substring(0, 3) + "XX";
			}
			else if (precision <= DecadePrecision)
			{
				return Data.Substring(0, 4) + "X";
			}
			else if (precision <= YearPrecision)
			{
				return Data.Substring(0, 5);
			}
			else if (precision <= MonthPrecision)
			{
				return Data.Substring(0, 8);
			}
			else if (precision <= DayPrecision)
			{
				return Data.Substring(0, 11);
			}
			//TODO: time precision
			else
			{
				return Data;
			}
		}

		public string GetYearString()
		{
			return GetString(YearPrecision);
		}

		public static string GetYearStringSafe(DateTime dateTime)
		{
			return dateTime == null ? "<none>" : dateTime.GetYearString();
		}

		/// <summary>
		/// Returns the latest possible year this Date could be in.
		/// </summary>
		public int GetLatestYear()
		{
			int year = int.Parse(Data.Substring(0, 5));

			if (Precision < MilleniumPrecision)
			{
				Debug.Assert(false, "Precision less than millenium");
				return year;
			}
			else if (Precision == MilleniumPrecision)
			{
				// millenia go from 1001-2000
				float yearFloat = year / 1000f;
				if (yearFloat < 0)
					year = (int)Math.Floor(yearFloat);
				else
					year = (int)Math.Ceiling(yearFloat);
				return year * 1000 - 1;
			}
			else if (Precision == CenturyPrecision)
			{
				// centuries go from 101-200
				float yearFloat = year / 100f;
				if (yearFloat < 0)
					year = (int)Math.Floor(yearFloat);
				else
					year = (int)Math.Ceiling(yearFloat);
				return year * 100 - 1;
			}
			else if (Precision == DecadePrecision)
			{
				// decades go from 10-19
				float yearFloat = year / 10f;
				year = (int)Math.Floor(yearFloat);
				return year * 10 + 9;
			}
			else
			{
				return year;
			}
		}

		public int GetYear()
		{
			return int.Parse(GetString(YearPrecision));
		}

		public int GetCentury()
		{
			return int.Parse(Data.Substring(1, 2)) + 1;
		}

		public override string ToString()
		{
			return GetString(SecondPrecision);
		}
	}
}
