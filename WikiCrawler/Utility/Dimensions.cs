using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public struct Dimensions
{
	public string Unit;
	public decimal Width;
	public decimal Height;

	public decimal Aspect
	{
		get { return Width / Height; }
	}

	public bool IsEmpty
	{
		get { return string.IsNullOrEmpty(Unit); }
	}

	public static readonly Dimensions Empty = new Dimensions("", 0, 0);

	public Dimensions(string unit, decimal width, decimal height)
	{
		Unit = unit;
		Width = width;
		Height = height;
	}

	public void MatchAspect(float aspect)
	{
		if ((aspect > 1 && Aspect < 1)
			|| (aspect < 1 && Aspect > 1))
		{
			decimal temp = Width;
			Width = Height;
			Width = temp;
		}
	}

	public string GetCommonsTag()
	{
		return "{{size|unit=" + Unit + "|width=" + Width.ToString() + "|height=" + Height.ToString() + "}}";
	}

	public Dimensions Flip()
	{
		return new Dimensions(Unit, Height, Width);
	}

	public static bool TryParse(string text, out Dimensions dimensions)
	{
		string[] dimSplit = text.Trim().Split(new string[] { " x " }, StringSplitOptions.None);
		if (dimSplit.Length == 2)
		{
			int lastSpace = dimSplit[1].LastIndexOf(' ');

			if (lastSpace >= 0)
			{
				string heightString = dimSplit[1].Substring(0, lastSpace);
				string unitString = dimSplit[1].Substring(lastSpace + 1);

				decimal width, height;
				string unit;
				if (TryParseNumber(dimSplit[0], out width)
					&& TryParseNumber(heightString, out height)
					&& TryParseUnit(unitString, out unit))
				{
					dimensions = new Dimensions(unit, width, height);
					return true;
				}
			}
		}
		dimensions = Empty;
		return false;
	}

	public static bool TryParseNumber(string text, out decimal number)
	{
		if (decimal.TryParse(text, out number))
		{
			return true;
		}

		// is it a fraction?
		string[] fracSplit = text.Split('/');
		if (fracSplit.Length == 2)
		{
			string first = fracSplit[0].Trim();
			string divisor = fracSplit[1].Trim();

			string[] firstSplit = first.Split(' ');
			string whole = null, divisee = null;
			if (firstSplit.Length == 1)
			{
				whole = "0";
				divisee = firstSplit[0];
			}
			else if (firstSplit.Length == 2)
			{
				whole = firstSplit[0];
				divisee = firstSplit[1];
			}

			if (!string.IsNullOrEmpty(divisee))
			{
				try
				{
					number = decimal.Parse(whole) + decimal.Parse(divisee) / decimal.Parse(divisor);
					return true;
				}
				catch (FormatException)
				{
					number = 0m;
					return false;
				}
			}
		}

		number = 0m;
		return false;
	}

	/// <summary>
	/// Attempts to parse the specified string into a unit.
	/// </summary>
	private static bool TryParseUnit(string raw, out string unit)
	{
		raw = raw.Trim(new char[] { '.', ' ' });
		switch (raw.ToLower())
		{
			case "cm":
			case "m":
			case "mm":
			case "in":
			case "ft":
				unit = raw.ToLower();
				return true;
			default:
				unit = "";
				return false;
		}
	}
}
