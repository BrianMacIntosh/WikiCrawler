using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Tasks.Commons;
using WikiCrawler;

public interface IBatchUploader : IBatchTask
{
	/// <summary>
	/// Checks the downloaded image file. Throws an exception if it's invalid and needs to be redownloaded.
	/// </summary>
	void ValidateDownload(string imagePath);
}

public abstract class BatchUploader<KeyType> : BatchTaskKeyed<KeyType>, IBatchUploader
{
	protected enum SuccessType
	{
		Failed,
		Succeeded,
		SkippedSucceeded,
	}

	protected Api Api = GlobalAPIs.Commons;

	/// <summary>
	/// Maps author strings to creators for this task.
	/// </summary>
	protected ManualMapping<MappingCreator> creatorMapping;

	public string CreatorMappingFile
	{
		get { return Path.Combine(ProjectDataDirectory, "creators.json"); }
	}

	public string PreviewDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "preview"); }
	}

	public string GetPreviewFileFilename(KeyType key)
	{
		return Path.Combine(PreviewDirectory, key + ".txt");
	}

	private WebClient WebClient;

	protected static string[] s_badTitleCharacters = new string[] { "/", "#" };

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

		WebClient = new WebClient();

		Api.AutoLogIn();

		creatorMapping = new ManualMapping<MappingCreator>(CreatorMappingFile);
	}

	private static bool s_stop = false;

	public override void Execute()
	{
		try
		{
			int keyCount = GetKeyCount();
			int initialSucceeded = m_succeeded.Count;
			int totalKeys = TotalKeyCount;
			int licenseFailures = 0;
			int uploadDeclined = 0;

			lock (Heartbeat)
			{
				if (totalKeys < 0)
				{
					// assume everything was downloaded
					Heartbeat.nTotal = keyCount + m_succeeded.Count - m_permanentlyFailed.Count;
				}
				else
				{
					Heartbeat.nTotal = totalKeys - m_permanentlyFailed.Count;
				}
				Heartbeat.nCompleted = m_succeeded.Count;
				Heartbeat.nDownloaded = keyCount - m_failMessages.Count - licenseFailures - uploadDeclined - (m_succeeded.Count - initialSucceeded);
				Heartbeat.nFailed = m_failMessages.Count;
				Heartbeat.nFailedLicense = licenseFailures;
				Heartbeat.nDeclined = uploadDeclined;
			}

			StartHeartbeat();

			using (FileSystemWatcher fileWatcher = new FileSystemWatcher(Configuration.DataDirectory, "STOP"))
			{
				fileWatcher.Created += OnStopFileCreated;
				fileWatcher.Renamed += OnStopFileCreated;
				fileWatcher.EnableRaisingEvents = true;
				string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");
				if (File.Exists(stopFile))
				{
					File.Delete(stopFile);
					return;
				}

				foreach (KeyType key in GetKeys())
				{
					try
					{
						if (ShouldAttemptKey(key))
						{
							Upload(key);
						}
					}
					catch (Exception e)
					{
						ConsoleUtility.WriteLine(ConsoleColor.Red, e.ToShortString());

						if (e is LicenseException)
						{
							licenseFailures++;
						}
						else if (e is UploadDeclinedException)
						{
							uploadDeclined++;
						}
						else
						{
							string failMessage = key.ToString().PadLeft(5) + "\t" + e.Message;
							m_failMessages.Add(failMessage);
						}
					}

					lock (Heartbeat)
					{
						Heartbeat.nCompleted = m_succeeded.Count;
						Heartbeat.nDownloaded = keyCount - m_failMessages.Count - licenseFailures - uploadDeclined - (m_succeeded.Count - initialSucceeded);
						Heartbeat.nFailed = m_failMessages.Count;
						Heartbeat.nFailedLicense = licenseFailures;
						Heartbeat.nDeclined = uploadDeclined;
					}

					if (s_stop)
					{
						return;
					}
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
	/// Returns all the keys that should be uploaded.
	/// </summary>
	public virtual IEnumerable<KeyType> GetKeys()
	{
		List<string> metadataFiles = Directory.GetFiles(MetadataCacheDirectory).ToList();

		if (m_config.randomizeOrder)
		{
			metadataFiles.Shuffle();
		}

		foreach (string metadataFile in metadataFiles)
		{
			KeyType key = StringToKey(Path.GetFileNameWithoutExtension(metadataFile));
			yield return key;
		}
	}

	/// <summary>
	/// Returns the number of keys to be uploaded.
	/// </summary>
	public virtual int GetKeyCount()
	{
		return GetKeys().Count();
	}

	public virtual bool ShouldAttemptKey(KeyType key)
	{
		return true;
	}

	public override void SaveOut()
	{
		base.SaveOut();

		creatorMapping.Serialize();
	}

	void OnStopFileCreated(object sender, FileSystemEventArgs e)
	{
		s_stop = true;
		File.Delete(e.FullPath);
		Console.WriteLine("Received STOP signal.");
	}

	public void Upload(KeyType key)
	{
		Console.WriteLine("== BUILDING " + key);

		Dictionary<string, string> metadata = LoadMetadata(key);

		PageTitle pageTitle = GetTitle(key, metadata);
		pageTitle.Name = pageTitle.Name.Replace(s_badTitleCharacters, "");

		Article art = new Article(pageTitle);
		art.revisions = new Revision[1];
		art.revisions[0] = new Revision();
		art.revisions[0].text = BuildPage(key, metadata);

		// permanently skip files
		if (ShouldSkipForever(key, metadata))
		{
			Console.WriteLine("Permanently skipping");
			m_permanentlyFailed.Add(key);
			DeleteCachedFiles(key, metadata);
			return;
		}

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

		if (!m_config.allowUpload)
		{
			throw new UWashException("upload disabled");
		}

		string imagePath = GetImageCacheFilename(key, metadata);

		if (m_config.manualApproval)
		{
			WindowsUtility.FlashWindowEx(Process.GetCurrentProcess().MainWindowHandle);

			// open up the preview and image
			Process.Start(imagePath);
			Process.Start(previewFile);

			Console.Write("Approve? (y/n): ");
			if (Console.ReadLine() != "y")
			{
				throw new UWashException("skipped");
			}
		}

		//TODO: check for existing extension
		art.title.Name += Path.GetExtension(imagePath);

		// creates any pages that the new page will be dependent on
		PreUpload(key, art);

		SuccessType uploadSuccess;
		try
		{
			string editMessage = "Batch upload";
			if (!string.IsNullOrEmpty(m_config.projectPage))
			{
				editMessage += " ([[" + m_config.projectPage + "]])";
			}
			uploadSuccess = Api.UploadFromLocal(art, imagePath, editMessage, true) ? SuccessType.Succeeded : SuccessType.Failed;
		}
		catch (DuplicateFileException e)
		{
			if (e.Duplicates.Length == 1 && e.IsSelfDuplicate)
			{
				// It looks like we already did this one
				uploadSuccess = SuccessType.SkippedSucceeded;
			}
			else if (e.Duplicates.Length == 1 && TryAddDuplicate(e.DuplicateTitles.First(), key, metadata))
			{
				uploadSuccess = SuccessType.SkippedSucceeded;
			}
			else if (m_config.succeedManualDupes)
			{
				uploadSuccess = SuccessType.SkippedSucceeded;
			}
			else
			{
				throw new UWashException(e.Message);
			}
		}
		catch (WikimediaException e)
		{
			throw new UWashException(e.Message);
		}

		if (uploadSuccess != SuccessType.Failed)
		{
			// failures in PostUpload will have to be fixed manually for now
			m_succeeded.Add(key);

			if (uploadSuccess == SuccessType.Succeeded)
			{
				PostUpload(key, metadata, art);
			}
		}
		else
		{
			throw new UWashException("upload failed");
		}

		DeleteCachedFiles(key, metadata);
	}

	/// <summary>
	/// Returns the total number of keys that need to be downloaded, or -1 if unknown.
	/// </summary>
	public virtual int TotalKeyCount
	{
		get { return -1; }
	}

	/// <summary>
	/// If the image for the specified item isn't cached, caches it.
	/// </summary>
	public virtual void CacheImage(KeyType key, Dictionary<string, string> metadata)
	{
		if (m_config.allowImageDownload)
		{
			string imagepath = GetImageCacheFilename(key, metadata);
			if (!File.Exists(imagepath))
			{
				Console.WriteLine("Downloading image: " + key);
				Uri uri = GetImageUri(key, metadata);
				WebThrottle.Get(uri).WaitForDelay();
				WebClient.Headers.Add("user-agent", Api.UserAgent);
				WebClient.DownloadFile(uri, imagepath);
				try
				{
					ValidateDownload(imagepath);
				}
				catch (Exception)
				{
					File.Delete(imagepath);
					throw;
				}
			}
		}
	}

	private void DeleteCachedFiles(KeyType key, Dictionary<string, string> metadata)
	{
		string path = GetImageCacheFilename(key, metadata);
		if (File.Exists(path))
		{
			File.Delete(path);
		}

		path = GetImageCroppedFilename(key, metadata);
		if (File.Exists(path))
		{
			File.Delete(path);
		}

		if (!Directory.Exists(MetadataTrashDirectory))
		{
			Directory.CreateDirectory(MetadataTrashDirectory);
		}

		path = GetMetadataCacheFilename(key);
		if (File.Exists(path))
		{
			File.Move(path, GetMetadataTrashFilename(key));
		}

		path = GetPreviewFileFilename(key);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	/// <summary>
	/// Loads the key-value metadata for the asset with the specified key.
	/// </summary>
	/// <param name="always">If set, will look in the trash and TODO: redownload if necessary.</param>
	public virtual Dictionary<string, string> LoadMetadata(KeyType key, bool always = false)
	{
		string cacheFile = GetMetadataCacheFilename(key);
		if (File.Exists(cacheFile))
		{
			return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(cacheFile, Encoding.UTF8));
		}

		string trashFile = GetMetadataTrashFilename(key);
		if (File.Exists(trashFile))
		{
			return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(trashFile, Encoding.UTF8));
		}

		return null;
	}

	/// <summary>
	/// Returns the URL for the item with the specified data.
	/// </summary>
	public abstract Uri GetImageUri(KeyType key, Dictionary<string, string> metadata);

	/// <summary>
	/// Returns the title of the uploaded page for the specified metadata.
	/// </summary>
	public abstract PageTitle GetTitle(KeyType key, Dictionary<string, string> metadata);

	/// <summary>
	/// Builds the wiki page for the object with the specified metadata.
	/// </summary>
	protected abstract string BuildPage(KeyType key, Dictionary<string, string> metadata);

	/// <summary>
	/// Attempts to merge this new page's information with an existing duplicate.
	/// </summary>
	/// <param name="targetPage">The existing page to merge into.</param>
	/// <returns>True on success.</returns>
	protected virtual bool TryAddDuplicate(PageTitle targetPage, KeyType key, Dictionary<string, string> metadata)
	{
		return false;
	}

	/// <summary>
	/// Returns true if the specified item should be marked as complete and not uploaded.
	/// </summary>
	public virtual bool ShouldSkipForever(KeyType key, Dictionary<string, string> metadata)
	{
		return false;
	}

	/// <summary>
	/// Checks the downloaded image file. Throws an exception if it's invalid and needs to be redownloaded.
	/// </summary>
	public virtual void ValidateDownload(string imagePath)
	{
		if (new FileInfo(imagePath).Length == 0)
		{
			throw new Exception("Downloaded empty file");
		}
	}

	/// <summary>
	/// Run any additional logic immediately before the article is uploaded (such as creating dependency pages).
	/// </summary>
	protected virtual void PreUpload(KeyType key, Article article)
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

	/// <summary>
	/// Run any additional logic after a successful upload (such as uploading a crop).
	/// </summary>
	protected virtual void PostUpload(KeyType key, Dictionary<string, string> metadata, Article article)
	{

	}

	#region Parse Helpers

	/// <summary>
	/// Returns the appropriate category check tag.
	/// </summary>
	protected static string GetCheckCategoriesTag(IList<string> categories)
	{
		return GetCheckCategoriesTag(categories.Count);
	}

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
	protected virtual string GetAuthor(string fileKey, string name, string lang, ref List<MappingCreator> creators)
	{
		string finalResult = "";
		foreach (string author in ParseAuthor(name))
		{
			if (creators == null)
			{
				creators = new List<MappingCreator>();
			}

			MappingCreator creator = creatorMapping.TryMapValue(author, fileKey);
			if (creator == null)
			{
				// only happens for invalid input
				continue;
			}
			else if (!creators.AddUnique(creator))
			{
				// duplicate
				continue;
			}
			else if (!string.IsNullOrEmpty(creator.MappedValue))
			{
				finalResult += creator.MappedValue;
				continue;
			}
			else if (!string.IsNullOrEmpty(creator.MappedQID))
			{
				CreatorTemplate qidCreator = WikidataCache.GetCreatorTemplate(QId.Parse(creator.MappedQID));
				if (!qidCreator.IsEmpty)
				{
					finalResult += qidCreator;
					continue;
				}
			}

			// if we get here, there is not a mapping for this creator; use it as plaintext
			if (!string.IsNullOrEmpty(lang))
			{
				finalResult += "{{" + lang + "|" + author + "}}";
			}
			else
			{
				//TODO: better support for tags with plaintext
				finalResult = StringUtility.Join("; ", finalResult, author);
			}
		}

		return finalResult;
	}

	private static char[] s_authorSplitters = { '|', '\n' };

	private static IEnumerable<string> ParseAuthor(string name)
	{
		string[] authors = name.Split(s_authorSplitters);
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

				// expect first names to only have one word
				//TODO: more verification that this is an actual name
				if (first.Count((chr) => chr == ' ') == 0)
				{
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

	protected string GetLicenseTag(string author, List<MappingCreator> creator, int latestYear, string pubCountry)
	{
		if (author == "{{unknown|author}}")
		{
			return LicenseUtility.GetPdLicenseTagUnknownAuthor(latestYear, pubCountry);
		}
		else if (author == "{{anonymous}}")
		{
			return LicenseUtility.GetPdLicenseTagAnonymousAuthor(latestYear, pubCountry);
		}
		else if (creator != null && creator.Any())
		{
			//TODO: manual per-author license override feature
			//if (creator.Count == 1 && !string.IsNullOrEmpty(creator[0].LicenseTemplate))
			//{
			//	return creator[0].LicenseTemplate;
			//}
			//else
			{
				int deathYearMax = creator.Max((MappingCreator c) => c.MappedDeathyear);
				return LicenseUtility.GetPdLicenseTag(latestYear, deathYearMax, pubCountry);
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
