using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wikimedia
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

		public string GetString(int maxPrecision)
		{
			int precision = Precision;

			if (precision > maxPrecision) precision = maxPrecision;

			if (precision <= MilleniumPrecision)
			{
				return Data[0] + "000";
			}
			else if (precision <= CenturyPrecision)
			{
				return Data.Substring(1, 2) + "00";
			}
			else if (precision <= DecadePrecision)
			{
				return Data.Substring(1, 3) + "0";
			}
			else if (precision <= YearPrecision)
			{
				return Data.Substring(1, 4);
			}
			else if (precision <= MonthPrecision)
			{
				return Data.Substring(1, 7);
			}
			else if (precision <= DayPrecision)
			{
				return Data.Substring(1, 10);
			}
			//TODO: time precision
			else
			{
				return Data.Substring(1);
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
	}
}
