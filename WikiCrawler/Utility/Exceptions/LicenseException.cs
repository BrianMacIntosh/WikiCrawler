using System;

/// <summary>
/// Thrown when attempting to upload a file without an apparent free license.
/// </summary>
public class LicenseException : Exception
{
	public override string Message
	{
		get { return "Could not determine a free license (" + Country + " " + Year.ToString() + "."; }
	}

	public readonly int Year;

	public readonly string Country;

	public LicenseException()
	{

	}

	public LicenseException(int year, string country)
	{
		Year = year;
		Country = country;
	}
}
