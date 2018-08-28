using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWash
{
	public static class UWashCountry
	{
		public static bool UseEUNoAuthor(string country)
		{
			switch (country)
			{
				//case "AUT":
				//case "BEL":
				//case "BGR":
				//case "HRV":
				//case "CYP":
				case "CZE":
				//case "DNK":
				//case "EST":
				//case "FIN":
				case "FRA":
				case "DEU":
				//case "GRC":
				//case "HUN":
				//case "IRL":
				//case "ITA":
				//case "LVA":
				//case "LTU":
				//case "LUX":
				//case "MLT":
				case "NLD":
				//case "POL":
				//case "PRT":
				//case "ROU":
				//case "SVK":
				//case "SVN":
				//case "ESP":
				//case "SWE":
				case "GBR":
					return true;
				default:
					return false;
			}
		}

		public static int GetPMADuration(string country)
		{
			switch (country)
			{
				case "GBR":
				case "FRA":
					return 70;
				default:
					return 9999;
			}
		}

		public static string Parse(string location)
		{
			string[] split = location.Split(new string[] { "--" }, StringSplitOptions.RemoveEmptyEntries);
			switch (split[0].Trim().ToUpper())
			{
				case "ENGLAND":
					return "GBR";
				case "FRANCE":
					return "FRA";
				default:
					return "";
			}
		}
	}
}
