using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

public static class WebInterface
{
	public static Stream HttpGet(Uri uri, WebThrottle throttle = null)
	{
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
		request.UserAgent = "BMacZeroBot (brianamacintosh@gmail.com) (for Wikimedia)";
		return HttpGet(request, throttle);
	}

	public static Stream HttpGet(HttpWebRequest request, WebThrottle throttle = null)
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

	public static Stream HttpPost(Func<HttpWebRequest> requestFactory, Dictionary<string, string> data, WebThrottle throttle = null)
	{
		string dataString = "";
		foreach (KeyValuePair<string, string> pair in data)
		{
			if (!string.IsNullOrEmpty(dataString))
			{
				dataString += "&";
			}
			dataString += pair.Key + "=" + System.Web.HttpUtility.UrlEncode(pair.Value);
		}
		return HttpPost(requestFactory, dataString, throttle);
	}

	public static Stream HttpPost(Func<HttpWebRequest> requestFactory, string data, WebThrottle throttle = null)
	{
		int retryCount = 0;

		byte[] datainflate = Encoding.UTF8.GetBytes(data);
	retry:
		HttpWebRequest request = requestFactory();
		request.Method = "POST";
		request.ContentType = "application/x-www-form-urlencoded";

		if (throttle == null)
		{
			throttle = WebThrottle.Get(request.RequestUri);
		}
		throttle.WaitForDelay();

		using (Stream newStream = request.GetRequestStream())
		{
			newStream.Write(datainflate, 0, datainflate.Length);
		}

		try
		{
			return request.GetResponse().GetResponseStream();
		}
		catch (WebException e)
		{
			HttpStatusCode statusCode = ((HttpWebResponse)e.Response).StatusCode;
			if (retryCount < 5
				&& (statusCode == HttpStatusCode.BadGateway || statusCode == HttpStatusCode.ServiceUnavailable))
			{
				Thread.Sleep(retryCount * 1000);
				retryCount++;
				goto retry;
			}
			throw;
		}
	}
}
