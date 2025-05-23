﻿using System;

/// <summary>
/// Contains utility functions for interacting with the Wayback Machine.
/// </summary>
public static class Wayback
{
	private const string QueryUrl = "https://archive.org/wayback/available?url={0}";

	private const string ArchiveUrl = "https://web.archive.org/save/{0}";

	/// <summary>
	/// Creates an archive of the specified page.
	/// </summary>
	public static void SavePage(string url)
	{
		url = url.Replace(":", "%3A");
		Uri archiveUri = new Uri(string.Format(ArchiveUrl, url));
		WebInterface.HttpGet(archiveUri);
	}

	public static string GetWaybackTemplate(string url, string text, string timestamp)
	{
		//TODO: escape pipes and =?
		return string.Format("{{Wayback|{0}|{1}|date={2}}}", url, text, timestamp);
	}
}
