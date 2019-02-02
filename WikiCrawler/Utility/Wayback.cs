﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format(ArchiveUrl, url));
		request.UserAgent = "BMacZeroBot (wikimedia)";
		EasyWeb.GetResponseStream(request);
	}

	public static string GetWaybackTemplate(string url, string text, string timestamp)
	{
		//TODO: escape
		return string.Format("{{Wayback|{0}|{1}|date={2}}}", url, text, timestamp);
	}
}
