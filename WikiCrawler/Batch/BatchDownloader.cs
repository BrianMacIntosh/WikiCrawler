using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Tasks;
using WikiCrawler;

public interface IBatchDownloader : IBatchTask
{

}

public abstract class BatchDownloader<KeyType> : BatchTaskKeyed<KeyType>, IBatchDownloader
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
	protected abstract IEnumerable<KeyType> GetKeys();

	/// <summary>
	/// Returns the URL for the item with the specified key.
	/// </summary>
	protected abstract Uri GetItemUri(KeyType key);

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

	public override void Execute()
	{
		string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");

		try
		{
			HashSet<KeyType> downloadSucceededKeys = GetDownloadSucceededKeys();

			int saveOutInterval = 10;
			int saveOutTimer = saveOutInterval;

			IEnumerable<KeyType> allKeys = GetKeys();
			int totalKeyCount = allKeys.Count();
			Heartbeat.nTotal = totalKeyCount - m_permanentlyFailed.Count;
			Heartbeat.nCompleted = m_succeeded.Count;
			Heartbeat.nDownloaded = downloadSucceededKeys.Count;
			Heartbeat.nFailed = m_failMessages.Count;
			Heartbeat.nFailedLicense = 0;

			StartHeartbeat();

			// load metadata
			IEnumerable<KeyType> downloadKeys = allKeys.Where(
				key => !downloadSucceededKeys.Contains(key)
					&& !m_permanentlyFailed.Contains(key));
			int downloadCount = downloadKeys.Count();
			foreach (KeyType key in downloadKeys)
			{
				Dictionary<string, string> metadata = Download(key);
				if (metadata != null)
				{
					downloadSucceededKeys.Add(key);
				}
				saveOutTimer--;

				lock (m_heartbeatTasks)
				{
					Heartbeat.nTotal = totalKeyCount - m_permanentlyFailed.Count;
					Heartbeat.nCompleted = m_succeeded.Count;
					Heartbeat.nDownloaded = downloadSucceededKeys.Count;
					Heartbeat.nFailed = m_failMessages.Count;
					Heartbeat.nFailedLicense = 0;
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

	protected virtual HashSet<KeyType> GetDownloadSucceededKeys()
	{
		// load the list of metadata that has already been downloaded
		HashSet<KeyType> downloadSucceededKeys = new HashSet<KeyType>();
		foreach (string cachedData in Directory.GetFiles(MetadataCacheDirectory))
		{
			downloadSucceededKeys.Add(StringToKey(Path.GetFileNameWithoutExtension(cachedData)));
		}

		// load the list of files that have already been uploaded
		if (!m_config.redownloadSucceeded)
		{
			downloadSucceededKeys.AddRange(m_succeeded);
		}

		return downloadSucceededKeys;
	}

	/// <summary>
	/// Downloads the data for the specified key.
	/// </summary>
	/// <returns>The parsed data.</returns>
	public Dictionary<string, string> Download(KeyType key, bool cache = true)
	{
		if (!m_config.allowDataDownload)
		{
			throw new Exception("Data download disabled");
		}
		Console.WriteLine("Downloading metadata: " + key);
		Uri url = GetItemUri(key);
	redownload:
		string content = Download(url);
		if (content == null)
		{
			// downloader requested skip
			Console.WriteLine("Downloader requested PERMANENT skip.");
			m_permanentlyFailed.Add(key);
			return null;
		}

		Dictionary<string, string> metadata;
		try
		{
			metadata = ParseMetadata(content);
		}
		catch (RedownloadException)
		{
			// parser requested redownload
			goto redownload;
		}
		catch (Exception e)
		{
			ConsoleUtility.WriteLine(ConsoleColor.Red, e.ToShortString());
			m_failMessages.Add(key.ToString().PadLeft(5) + "\t" + e.Message);
			return null;
		}

		if (metadata == null)
		{
			Console.WriteLine("Parser requested PERMANENT skip.");
			m_permanentlyFailed.Add(key);
		}
		else if (cache)
		{
			File.WriteAllText(
				GetMetadataCacheFilename(key),
				JsonConvert.SerializeObject(metadata, Formatting.Indented),
				Encoding.UTF8);
		}
		return metadata;
	}

	/// <summary>
	/// Downloads the specified URL.
	/// </summary>
	private string Download(Uri url)
	{
		//Read HTML data
		do
		{
			try
			{
				using (StreamReader read = new StreamReader(WebInterface.HttpGet(url)))
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
				else if (e.Response != null)
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
						Console.WriteLine("Other Error - Retrying");
						System.Threading.Thread.Sleep(60000);
					}
				}
				else
				{
					Console.WriteLine("Other Error - Retrying");
					System.Threading.Thread.Sleep(60000);
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
