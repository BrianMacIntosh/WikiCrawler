using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class Utility
{
	public static string FormatOrdinal(int number)
	{
		int ones = number % 10;
		int tens = number % 100;
		switch (tens)
		{
			case 11:
			case 12:
			case 13:
				return number + "th";
		}
		switch (ones)
		{
			case 1:
				return number + "st";
			case 2:
				return number + "nd";
			case 3:
				return number + "rd";
		}
		return number + "th";
	}
}
