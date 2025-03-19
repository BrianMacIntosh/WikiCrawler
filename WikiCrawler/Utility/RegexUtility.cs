using System.Text.RegularExpressions;

/// <summary>
/// Contains utility function for interacting with regexes.
/// </summary>
public static class RegexUtility
{
	public static bool MatchOut(this Regex regex,  string pattern, out Match match)
	{
		match = regex.Match(pattern);
		return match.Success;
	}
}
