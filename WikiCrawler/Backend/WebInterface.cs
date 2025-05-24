using System;
using System.IO;
using System.Net;

public static class WebInterface
{
	public static Stream ReadHttpStream(Uri uri, WebThrottle throttle = null)
	{
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
		request.UserAgent = "BMacZeroBot (brianamacintosh@gmail.com) (for Wikimedia)";
		return ReadHttpStream(request, throttle);
	}

	public static Stream ReadHttpStream(HttpWebRequest request, WebThrottle throttle = null)
	{
		if (throttle == null)
		{
			throttle = WebThrottle.Get(request.RequestUri);
		}
		throttle.WaitForDelay();

		//TODO: check robots.txt
		//throw new InvalidOperationException("The site's robots.txt forbids access to '" + request.Address + "'.");

		HttpWebResponse response = (HttpWebResponse)request.GetResponse();
		return response.GetResponseStream();
	}
}
