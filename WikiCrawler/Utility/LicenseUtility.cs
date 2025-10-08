using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

/// <summary>
/// Helper function for finding media licenses.
/// </summary>
public static class LicenseUtility
{
	/// <summary>
	/// A very safe estimate of the lifetime of an author after creating a work, used if the deathyear is not known.
	/// </summary>
	private const int SafeLifetimeYears = 80;

	public const int UnitedStatesExpiryTimeYears = 95;

	public static int UnitedStatesExpiryYear => System.DateTime.Now.Year - UnitedStatesExpiryTimeYears;

	private static readonly string[] s_primaryLicenseTemplates;

	private static int CurrentYear
	{
		get { return System.DateTime.Now.Year; }
	}

	static LicenseUtility()
	{
		s_primaryLicenseTemplates = File.ReadAllLines(Path.Combine(Configuration.DataDirectory, "primary-license-tags.txt"));
	}

	/// <summary>
	/// Returns an enumerator over the names of every primary license template (no {{}} or Template:).
	/// </summary>
	public static IEnumerable<string> PrimaryLicenseTemplates
	{
		get { return s_primaryLicenseTemplates; }
	}

	/// <summary>
	/// Returns true if the specified country can use the EU no-author license.
	/// </summary>
	private static bool UseEUNoAuthor(string country)
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

	/// <summary>
	/// Returns the post mortem auctoris duration in the specified country.
	/// </summary>
	public static int GetPMADuration(string country)
	{
		switch (country)
		{
			case "CAN":
			case "CHN":
				return 50;
			case "AUT":
			case "DEU":
			case "DNK":
			case "FRA":
			case "GBR":
			case "NLD":
			case "SWE":
			case "CHE":
				return 70;
			default:
				return 100;
		}
	}

	public static int GetPMADurationByQID(QId qid)
	{
		if (qid.IsEmpty)
		{
			return 100;
		}

		switch (qid.Id)
		{
			case 683: // Samoa
			case 774: // Guatemala
			case 757: // Saint Vincent and the Grenadines
				return 75;
			case 1006: // Guinea
			case 739: // Colombia
			case 983: // Equatorial Guinea
				return 80;
			case 766: // Jamaica
				return 95;
			case 96: // Mexico
				return 100;
			default:
				return 70; // conservative: all nations above 70 are listed above
		}
	}

	/// <summary>
	/// Parses a natural-language string into a country code.
	/// </summary>
	public static string ParseCountry(string location)
	{
		string[] split = location.Split(new string[] { "--" }, StringSplitOptions.RemoveEmptyEntries);
		switch (split[0].Trim().ToUpper())
		{
			case "AUSTRIA":
				return "AUT";
			case "BELGIUM":
				return "BEL";
			case "CANADA":
				return "CAN";
			case "CHINA":
				return "CHN";
			case "DENMARK":
				return "DNK";
			case "ENGLAND":
			case "GREAT BRITAIN":
			case "UNITED KINGDOM":
				return "GBR";
			case "FRANCE":
				return "FRA";
			case "GERMANY":
				return "DEU";
			case "ITALY":
				return "ITA";
			case "NETHERLANDS":
			case "THE NETHERLANDS":
				return "NLD";
			case "RUSSIA":
				return "RUS";
			case "SWEDEN":
				return "SWE";
			case "SWITZERLAND":
				return "CHE";
			case "UNITED STATES":
			case "UNITES STATES":
				return "USA";
			default:
				throw new Exception("Failed to parse country '" + location + "'");
		}
	}

	// <summary>
	/// Returns the best applicable license tag for an item with an unknown author.
	/// </summary>
	public static string GetPdLicenseTagUnknownAuthor(int pubYear, string pubCountry)
	{
		if (pubCountry == "USA")
		{
			pubCountry = "US";
			if (pubYear < (CurrentYear - UnitedStatesExpiryTimeYears))
			{
				return "{{PD-US-expired|country=" + pubCountry + "}}";
			}
			else
			{
				return "";
			}
		}
		else
		{
			return "";
		}
	}

	/// <summary>
	/// Returns the best applicable license tag for an item with an anonymous author.
	/// </summary>
	public static string GetPdLicenseTagAnonymousAuthor(int pubYear, string pubCountry)
	{
		if (UseEUNoAuthor(pubCountry))
		{
			if (CurrentYear - pubYear > 70)
			{
				return "{{PD-EU-no author disclosure}}";
			}
			else
			{
				return "";
			}
		}
		else if (pubCountry == "USA")
		{
			if (pubYear < (CurrentYear - UnitedStatesExpiryTimeYears))
			{
				return "{{PD-anon-expired}}";
			}
			else
			{
				return "";
			}
		}
		else
		{
			return "";
		}
	}

	/// <summary>
	/// Returns the best applicable license tag for an item with the specified parameters.
	/// </summary>
	public static string GetPdLicenseTag(int pubYear, int? authorDeathYear, string pubCountry)
	{
		bool canUsePDOldExpired = false;

		if (pubCountry == "USA")
		{
			canUsePDOldExpired = true;
		}
		else if (authorDeathYear.HasValue)
		{
			canUsePDOldExpired = (CurrentYear - authorDeathYear.Value) > GetPMADuration(pubCountry)
				// estimate for authors with unknown deathyear
				|| CurrentYear - (pubYear + SafeLifetimeYears) > GetPMADuration(pubCountry);
		}

		if (canUsePDOldExpired && pubYear < (CurrentYear - UnitedStatesExpiryTimeYears))
		{
			if (authorDeathYear.HasValue && authorDeathYear != 9999)
			{
				return "{{PD-old-auto-expired|deathyear=" + authorDeathYear.ToString() + "}}";
			}
			else if (pubCountry == "US" || pubCountry == "USA")
			{
				return "{{PD-US-expired|country=US}}";
			}
			else if (pubCountry == "FRA")
			{
				return "{{PD-US-expired}}{{PD-France}}";
			}
			else
			{
				return "";
			}
		}
		else
		{
			if (authorDeathYear.HasValue && CurrentYear - authorDeathYear >= 70)
			{
				return "";// "{{PD-old-auto|deathyear=" + authorDeathYear.ToString() + "}}";
			}
			else
			{
				return "";
			}
		}
	}
}
