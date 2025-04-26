using LightweightRobots;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

class EasyWeb
{
	/// <summary>
	/// Robots object to prohibit access to certain pages
	/// </summary>
	public static RobotsTxt robots
	{
		get { return mrobots; }
		set
		{
			mrobots = value;
			if (value.CrawlDelay() > 0) crawlDelay = value.CrawlDelay();
		}
	}
	private static RobotsTxt mrobots;

	/// <summary>
	/// Default time in seconds between page loads
	/// </summary>
	public static float crawlDelay = 5f;

	private static Dictionary<string, float> overrideDelays = new Dictionary<string, float>();

	private static Dictionary<string, Stopwatch> domainTimers = new Dictionary<string, Stopwatch>();

	public static Stream GetResponseStream(HttpWebRequest request)
	{
		if (robots == null || robots.IsAllowed(request.Address))
		{
			WaitForDelay(request.RequestUri);
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();
			return response.GetResponseStream();
		}
		else
		{
			throw new InvalidOperationException("The site's robots.txt forbids access to '" + request.Address + "'.");
		}
	}

	public static void SetDelayForDomain(Uri uri, float duration)
	{
		overrideDelays[uri.Host.ToLower()] = duration;
	}

	public static void WaitForDelay(Uri uri)
	{
		if (domainTimers.TryGetValue(uri.Host, out Stopwatch stopwatch))
		{
			float useDelay = crawlDelay;
			if (overrideDelays.ContainsKey(uri.Host.ToLower()))
				useDelay = overrideDelays[uri.Host.ToLower()];
			Thread.Sleep(Math.Max(0, (int)(useDelay * 1000 - stopwatch.ElapsedMilliseconds)));
			stopwatch.Restart();
		}
		else
		{
			domainTimers.Add(uri.Host, Stopwatch.StartNew());
		}
	}

	public static Stream Post(Func<HttpWebRequest> requestFactory, Dictionary<string, string> data)
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
		return Post(requestFactory, dataString);
	}

	public static Stream Post(Func<HttpWebRequest> requestFactory, string data)
	{
		byte[] datainflate = Encoding.UTF8.GetBytes(data);
	retry:
		HttpWebRequest request = requestFactory();
		request.Method = "POST";
		request.ContentType = "application/x-www-form-urlencoded";

		WaitForDelay(request.RequestUri);

		int retryCount = 0;

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

	public static Stream Upload(HttpWebRequest request, Dictionary<string, string> data,
		string filename, string filetype, string filekey, Stream filedata)
	{
		return MultipartUpload.UploadFile(request, data, filename, filetype, filekey, filedata);
	}

	public static Stream Upload(HttpWebRequest request, Dictionary<string, string> data,
		string filename, string filetype, string filekey, byte[] filedata)
	{
		return MultipartUpload.UploadFile(request, data, filename, filetype, filekey,
			filedata, 0, filedata.Length);
	}

	public static Stream Upload(HttpWebRequest request, Dictionary<string, string> data,
		string filename, string filetype, string filekey, byte[] filedata, int fileDataOffset, int fileDataLength)
	{
		return MultipartUpload.UploadFile(request, data, filename, filetype, filekey,
			filedata, fileDataOffset, fileDataLength);
	}
}