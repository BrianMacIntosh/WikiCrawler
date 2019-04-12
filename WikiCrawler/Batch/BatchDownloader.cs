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

			// load metadata
			foreach (string key in GetKeys())
			{
				// download metadata
				if (!succeededMetadata.Contains(key) && !m_succeeded.Contains(key))
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
						else
						{
							File.WriteAllText(
								GetMetadataCacheFilename(key),
								JsonConvert.SerializeObject(metadata, Formatting.Indented),
								Encoding.UTF8);
						}
					}
					catch (RedownloadException)
					{
						goto redownload;
					}

					saveOutTimer--;
				}

				if (File.Exists(stopFile))
				{
					File.Delete(stopFile);
					Console.WriteLine("Received STOP signal.");
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
					throw;
					//Console.WriteLine("Timeout - sleeping (30 sec)");
					//System.Threading.Thread.Sleep(30000);
				}
				else
				{
					HttpWebResponse response = (HttpWebResponse)e.Response;
					if (response.StatusCode == HttpStatusCode.NotFound)
					{
						Console.WriteLine("404 error encountered, skipping file");
						return null;
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
