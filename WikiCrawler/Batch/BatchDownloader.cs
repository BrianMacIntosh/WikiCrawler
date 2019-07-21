using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WikiCrawler;

public abstract class BatchDownloader : BatchTask
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="key">The name of the directory storing the project data.</param>
	public BatchDownloader(string key)
		: base(key)
	{
		if (!Directory.Exists(ImageCacheDirectory))
			Directory.CreateDirectory(ImageCacheDirectory);

		if (!Directory.Exists(MetadataCacheDirectory))
			Directory.CreateDirectory(MetadataCacheDirectory);
	}

	/// <summary>
	/// Enumerates the list of all available item keys.
	/// </summary>
	protected abstract IEnumerable<string> GetKeys();

	/// <summary>
	/// Returns the URL for the item with the specified key.
	/// </summary>
	protected abstract Uri GetItemUri(string key);

	/// <summary>
	/// Parses metadata from a downloaded page.
	/// </summary>
	/// <returns>Null if there was none and we shouldn't try again,</returns>
	/// <exception cref="UWashException">There was a problem and we should try again.</exception>
	protected abstract Dictionary<string, string> ParseMetadata(string pageContent);

	/// <summary>
	/// If set, the task will stop.
	/// </summary>
	protected bool Finished = false;

	/// <summary>
	/// Downloads all metadata and images.
	/// </summary>
	public void DownloadAll()
	{
		string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");

		try
		{
			// load the list of metadata that has already been downloaded
			HashSet<string> succeededMetadata = new HashSet<string>();
			foreach (string cachedData in Directory.GetFiles(MetadataCacheDirectory))
			{
				succeededMetadata.Add(Path.GetFileNameWithoutExtension(cachedData));
			}

			int saveOutInterval = 10;
			int saveOutTimer = saveOutInterval;

			m_heartbeatData["nTotal"] = GetKeys().Count();
			StartHeartbeat();

			// load metadata
			foreach (string key in GetKeys())
			{
				if (!succeededMetadata.Contains(key) && !m_succeeded.Contains(key))
				{
					Dictionary<string, string> metadata = Download(key);
					if (metadata != null)
					{
						succeededMetadata.Add(key);
					}
					saveOutTimer--;
				}

				lock (m_heartbeatData)
				{
					m_heartbeatData["nCompleted"] = m_succeeded.Count;
					m_heartbeatData["nDownloaded"] = succeededMetadata.Count;
					m_heartbeatData["nFailed"] = m_failMessages.Count;
					m_heartbeatData["nFailedLicense"] = 0;
				}
				
				if (File.Exists(stopFile))
				{
					File.Delete(stopFile);
					Console.WriteLine("Received STOP signal.");
					return;
				}
				else if (Finished)
				{
					return;
				}

				if (saveOutTimer <= 0)
				{
					SaveOut();
					saveOutTimer = saveOutInterval;
				}
			}
		}
		finally
		{
			SaveOut();
			SendHeartbeat(true);
		}
	}

	/// <summary>
	/// Downloads the data for the specified key.
	/// </summary>
	/// <returns>The parsed data.</returns>
	public Dictionary<string, string> Download(string key, bool cache = true)
	{
		Console.WriteLine("Downloading metadata: " + key);
		Uri url = GetItemUri(key);
	redownload:
		string content = Download(url);
		try
		{
			Dictionary<string, string> metadata = ParseMetadata(content);
			if (metadata == null)
			{
				m_succeeded.Add(key);
			}
			else if (cache)
			{
				//TODO: crash when failing here
				File.WriteAllText(
					GetMetadataCacheFilename(key),
					JsonConvert.SerializeObject(metadata, Formatting.Indented),
					Encoding.UTF8);
			}
			return metadata;
		}
		catch (RedownloadException)
		{
			goto redownload;
		}
		catch (Exception e)
		{
			Console.WriteLine(e.ToString());
			m_failMessages.Add(key.PadLeft(5) + "\t" + e.Message);
			return null;
		}
	}

	/// <summary>
	/// Downloads the specified URL.
	/// </summary>
	private string Download(Uri url)
	{
		//Read HTML data
		do
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.UserAgent = "brian@brianmacintosh.com (Wikimedia Commons) - bot";
			try
			{
				using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
				{
					return read.ReadToEnd();
				}
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.Timeout)
				{
					Console.WriteLine("Timeout - Retrying");
					System.Threading.Thread.Sleep(30000);
				}
				else
				{
					HttpWebResponse response = (HttpWebResponse)e.Response;
					if (response.StatusCode == HttpStatusCode.NotFound)
					{
						Console.WriteLine("404 error encountered, skipping file");
						return null;
					}
					else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
					{
						Console.WriteLine("Service Unavailable - Retrying");
						System.Threading.Thread.Sleep(60000);
					}
					else
					{
						throw;
					}
				}
			}
			catch (IOException e)
			{
				Console.WriteLine(e);
				Console.WriteLine();
				Console.WriteLine("Sleeping (30 sec)");
				System.Threading.Thread.Sleep(30000);
			}
		} while (true);
	}
}
