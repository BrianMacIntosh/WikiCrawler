using LightweightRobots;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

/// <summary>
/// Object that throttles web requests to a specific host.
/// </summary>
public class WebThrottle
{
	/// <summary>
	/// The host this accessor accesses.
	/// </summary>
	public readonly string Host;

	/// <summary>
	/// Minimum time in seconds between page requests.
	/// </summary>
	public float CrawlDelay = 5f;

	private Stopwatch m_delayTimer = new Stopwatch();

	public static Dictionary<string, WebThrottle> StaticThrottles = new Dictionary<string, WebThrottle>();

	public static WebThrottle Get(Uri uri)
	{
		string host = uri.Host;
		if (StaticThrottles.TryGetValue(host, out WebThrottle accessor))
		{
			return accessor;
		}
		else
		{
			accessor = new WebThrottle(host);
			StaticThrottles[host] = accessor;
			return accessor;
		}
	}

	public WebThrottle(string host, float crawlDelay = 5f)
	{
		Host = host;
		CrawlDelay = crawlDelay;
	}

	public void WaitForDelay()
	{
		Thread.Sleep(Math.Max(0, (int)(CrawlDelay * 1000 - m_delayTimer.ElapsedMilliseconds)));
		m_delayTimer.Restart();
	}

	//TODO: move to WebInterface
	public Stream Upload(HttpWebRequest request, Dictionary<string, string> data,
		string filename, string filetype, string filekey, byte[] filedata)
	{
		WaitForDelay();
		return MultipartUpload.UploadFile(request, data, filename, filetype, filekey,
			filedata, 0, filedata.Length);
	}

	//TODO: move to WebInterface
	public Stream Upload(HttpWebRequest request, Dictionary<string, string> data,
		string filename, string filetype, string filekey, byte[] filedata, int fileDataOffset, int fileDataLength)
	{
		WaitForDelay();
		return MultipartUpload.UploadFile(request, data, filename, filetype, filekey,
			filedata, fileDataOffset, fileDataLength);
	}
}