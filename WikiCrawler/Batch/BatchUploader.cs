using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using WikiCrawler;
using Wikimedia;

public abstract class BatchUploader : BatchTask
{
	protected WikiApi Api = new WikiApi(new Uri("https://commons.wikimedia.org/"));

	protected string PreviewDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "preview"); }
	}

	protected string GetPreviewFileFilename(string key)
	{
		return Path.Combine(PreviewDirectory, key + ".txt");
	}

	private static WebClient s_client = new WebClient();

	public BatchUploader(string key)
		: base(key)
	{
		if (!Directory.Exists(PreviewDirectory))
		{
			Directory.CreateDirectory(PreviewDirectory);
		}
		if (!Directory.Exists(ImageCacheDirectory))
		{
			Directory.CreateDirectory(ImageCacheDirectory);
		}

		Console.WriteLine("Logging in...");
		Credentials credentials = Configuration.LoadCredentials();
		Api.LogIn(credentials.Username, credentials.Password);

		CreatorUtility.Initialize(Api);
	}

	/// <summary>
	/// Uploads all configured files.
	/// </summary>
	public void UploadAll()
	{
		string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");

		StreamWriter errorsWriter = new StreamWriter(new FileStream(Path.Combine(ProjectDataDirectory, "failed.txt"), FileMode.Create, FileAccess.Write));
		try
		{
			foreach (string metadataFile in Directory.GetFiles(MetadataCacheDirectory))
			{
				string key = Path.GetFileNameWithoutExtension(metadataFile);

				try
				{
					Upload(metadataFile);
				}
				catch (UWashException e)
				{
					errorsWriter.WriteLine(key + "\t\t" + e.Message);
					errorsWriter.Flush();
				}

				if (File.Exists(stopFile))
				{
					File.Delete(stopFile);
					Console.WriteLine("Received STOP signal.");
					return;
				}
			}
		}
		finally
		{
			errorsWriter.Close();
			SaveOut();
		}
	}

	public void Upload(string metadataFile)
	{
		string key = Path.GetFileNameWithoutExtension(metadataFile);

		Console.WriteLine("== BUILDING " + key);

		Dictionary<string, string> metadata
			= JsonConvert.DeserializeObject<Dictionary<string, string>>(
				File.ReadAllText(metadataFile, Encoding.UTF8));

		Article art = new Article();
		art.title = GetTitle(key, metadata).Replace("/", "");
		art.revisions = new Revision[1];
		art.revisions[0] = new Revision();
		art.revisions[0].text = BuildPage(key, metadata);

		string previewFile = GetPreviewFileFilename(key);
		using (StreamWriter writer = new StreamWriter(new FileStream(previewFile, FileMode.Create)))
		{
			writer.Write(art.revisions[0].text);
		}

		try
		{
			CacheImage(key, metadata);
		}
		catch (WebException e)
		{
			HttpWebResponse webResponse = e.Response as HttpWebResponse;
			if (webResponse != null)
			{
				throw new UWashException("Failed to download source file (" + webResponse.StatusCode.ToString() + ")");
			}
			else
			{
				throw new UWashException("Failed to download source file");
			}
		}

		// also crops
		string imagePath = GetUploadImagePath(key, metadata);

		if (!m_config.allowUpload)
		{
			throw new UWashException("upload disabled");
		}

		//TODO: check for existing extension
		art.title += Path.GetExtension(imagePath);

		PreUpload(art);

		reupload:
		bool uploadSuccess;
		try
		{
			uploadSuccess = Api.UploadFromLocal(art, imagePath, "(BOT) batch upload", true);
		}
		catch (WikimediaException e)
		{
			throw new UWashException(e.Message);
		}
		catch (WebException e)
		{
			if (e.Status == WebExceptionStatus.ProtocolError
				&& ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.ServiceUnavailable)
			{
				System.Threading.Thread.Sleep(60000);
				goto reupload;
			}
			else
			{
				uploadSuccess = false;
			}
		}

		if (uploadSuccess)
		{
			m_succeeded.Add(key);
		}
		else
		{
			throw new UWashException("upload failed");
		}

		DeleteImageCache(key);
	}

	/// <summary>
	/// Saves out progress to a file.
	/// </summary>
	protected override void SaveOut()
	{
		base.SaveOut();
		CreatorUtility.SaveOut();
	}

	/// <summary>
	/// If the image for the specified item isn't cached, caches it.
	/// </summary>
	public void CacheImage(string key, Dictionary<string, string> metadata)
	{
		if (m_config.allowImageDownload)
		{
			string imagepath = GetImageCacheFilename(key);
			if (!File.Exists(imagepath))
			{
				Console.WriteLine("Downloading image: " + key);
				Uri uri = GetImageUri(key, metadata);
				EasyWeb.WaitForDelay(uri);
				s_client.DownloadFile(uri, imagepath);
			}
		}
	}

	private void DeleteImageCache(string key)
	{
		string path = GetImageCacheFilename(key);
		if (File.Exists(path))
		{
			File.Delete(path);
		}

		path = GetImageCroppedFilename(key);
		if (File.Exists(path))
		{
			File.Delete(path);
		}

		path = GetMetadataCacheFilename(key);
		if (File.Exists(path))
		{
			File.Delete(path);
		}

		path = GetPreviewFileFilename(key);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	/// <summary>
	/// Returns the URL for the item with the specified data.
	/// </summary>
	protected abstract Uri GetImageUri(string key, Dictionary<string, string> metadata);

	/// <summary>
	/// Returns the title of the uploaded page for the specified metadata.
	/// </summary>
	protected abstract string GetTitle(string key, Dictionary<string, string> metadata);

	/// <summary>
	/// Prepares the image for upload and returns the path to the file to upload.
	/// </summary>
	protected abstract string GetUploadImagePath(string key, Dictionary<string, string> metadata);

	/// <summary>
	/// Builds the wiki page for the object with the specified metadata.
	/// </summary>
	protected abstract string BuildPage(string key, Dictionary<string, string> metadata);

	/// <summary>
	/// Run any additional logic immediately before the article is uploaded.
	/// </summary>
	protected virtual void PreUpload(Article article)
	{
		// create categories for {{taken on}} if present
		int takenOnIndex = article.revisions[0].text.IndexOf("{{taken on|", StringComparison.CurrentCultureIgnoreCase);
		if (takenOnIndex >= 0)
		{
			int takenOnCloseIndex = article.revisions[0].text.IndexOf("}}", takenOnIndex);
			int dateStart = takenOnIndex + "{{taken on|".Length;
			string date = article.revisions[0].text.Substring(dateStart, takenOnCloseIndex - dateStart);
			CommonsUtility.EnsureTakenOnCategories(Api, date);
		}
	}

	#region Parse Helpers

	/// <summary>
	/// Returns the appropriate category check tag.
	/// </summary>
	protected static string GetCheckCategoriesTag(int categoryCount)
	{
		string dmy = "day=" + System.DateTime.Now.Day + "|month="
			+ CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(System.DateTime.Now.Month)
			+ "|year=" + System.DateTime.Now.Year;
		if (categoryCount <= 0)
		{
			return "{{uncategorized|" + dmy + "}}";
		}
		else
		{
			return "{{check categories|" + dmy + "|ncats=" + categoryCount + "}}";
		}
	}

	/// <summary>
	/// Get a string that should be used for the file's 'author' field.
	/// </summary>
	protected string GetAuthor(string name, string lang, out Creator creator)
	{
		//TODO: support multiple creators
		creator = null;

		string finalResult = "";
		foreach (string author in ParseAuthor(name))
		{
			if (CreatorUtility.TryGetCreator(author, out creator))
			{
				creator.Usage++;
				if (!string.IsNullOrEmpty(creator.Author))
				{
					finalResult += creator.Author;
					continue;
				}
			}

			// if we get here, there is not yet a mapping for this creator
			finalResult += "{{" + lang + "|" + author + "}}";
		}

		return finalResult;
	}

	private static IEnumerable<string> ParseAuthor(string name)
	{
		string[] authors = name.Split(StringUtility.Pipe);
		for (int c = 0; c < authors.Length; c++)
		{
			authors[c] = CleanPersonName(authors[c]);

			//try to unswitcheroo Last, First format
			string[] commasplit = authors[c].Split(',');
			if (commasplit.Length == 2)
			{
				string first = commasplit[1].Trim();
				string last = commasplit[0].Trim();
				string suffix = "";

				if (first.EndsWith("Jr.") || first.EndsWith("Sr."))
				{
					suffix = first.Substring(first.Length - 3).Trim();
					first = first.Remove(first.Length - 3).Trim();
				}

				string result = first + " " + last;
				if (!string.IsNullOrEmpty(suffix))
				{
					result += " " + suffix;
				}
				yield return result;
			}
			else
			{
				yield return authors[c];
			}
		}
	}

	private static char[] s_dobTrim = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', ' ', '?', ',' };

	/// <summary>
	/// Cleans DOB information from a name string.
	/// </summary>
	protected static string CleanPersonName(string name)
	{
		//Remove trailing DOB/lifetime
		//HACK: be more explicit with regex
		return name.TrimEnd(s_dobTrim);
	}

	protected string GetLicenseTag(string author, Creator creator, int latestYear, string pubCountry)
	{
		if (author == "{{unknown|author}}")
		{
			return LicenseUtility.GetPdLicenseTagUnknownAuthor(latestYear, pubCountry);
		}
		else if (author == "{{anonymous}}")
		{
			return LicenseUtility.GetPdLicenseTagAnonymousAuthor(latestYear, pubCountry);
		}
		else if (creator != null)
		{
			if (!string.IsNullOrEmpty(creator.LicenseTemplate))
			{
				return creator.LicenseTemplate;
			}
			else
			{
				return LicenseUtility.GetPdLicenseTag(latestYear, creator.DeathYear, pubCountry);
			}
		}
		else
		{
			return LicenseUtility.GetPdLicenseTag(latestYear, null, pubCountry);
		}
	}

	/// <summary>
	/// Parses an object's physical description (dimensions, medium).
	/// </summary>
	protected void ParsePhysicalDescription(string raw, out string medium, out Dimensions dimensions)
	{
		string[] split = raw.Split(';');
		for (int i = 0; i < split.Length; i++)
		{
			split[i] = split[i].Trim();

			// does this look like a dimension?
			if (Dimensions.TryParse(split[i], out dimensions))
			{
				// these are usually backwards
				//TODO: check image aspect ratio
				dimensions = dimensions.Flip();

				// the medium is everything else
				medium = "";
				for (int j = 0; j < split.Length; j++)
				{
					if (j != i)
					{
						medium = StringUtility.Join("; ", medium, split[j]);
					}
				}
				return;
			}
		}

		dimensions = Dimensions.Empty;
		medium = raw;
		return;
	}

	#endregion
}
