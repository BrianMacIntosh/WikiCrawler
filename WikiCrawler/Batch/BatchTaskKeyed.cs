using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;

public enum BatchItemStatus
{
	Unknown,
	NotDownloaded,
	PermanentlySkipped,
	Downloaded,
	Failed,
	LicenseFailed,
	UploadDeclined,
	Succeeded,

	COUNT
}

/// <summary>
/// Base class for a batch file upload or download task with uniquely-keyed files.
/// </summary>
/// <typeparam name="KeyType">The type of the unique key for files in this task.</typeparam>
public abstract class BatchTaskKeyed<KeyType> : BatchTask
{
	/// <summary>
	/// For each item, its status.
	/// </summary>
	protected Dictionary<KeyType, BatchItemStatus> m_itemStatus = new Dictionary<KeyType, BatchItemStatus>();

	public BatchTaskKeyed(string key)
		: base(key)
	{
		// load already-succeeded uploads
		if (GetPersistStatus())
		{
			string statusFile = Path.Combine(ProjectDataDirectory, "status.json");
			if (File.Exists(statusFile))
			{
				m_itemStatus = JsonConvert.DeserializeObject<Dictionary<KeyType, BatchItemStatus>>(File.ReadAllText(statusFile, Encoding.UTF8));

				return; // do not do any backwards-compat!
			}

			// mark all available keys as Downloaded
			//TODO:

			// load backwards-compatible success file
			string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
			if (File.Exists(succeededFile))
			{
				HashSet<KeyType> m_succeeded = new HashSet<KeyType>();

				string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(succeededFile, Encoding.UTF8));
				foreach (string succeededString in succeeded)
				{
					KeyType succeededKey = StringToKey(succeededString);
					m_succeeded.Add(succeededKey);
				}

				foreach (KeyType itemKey in m_succeeded)
				{
					m_itemStatus[itemKey] = BatchItemStatus.Succeeded;
				}

				File.Move(succeededFile, Path.Combine(Path.GetDirectoryName(succeededFile), "succeeded_old.json"));
			}

			// load backwards-compatible failed file
			string failedFile = Path.Combine(ProjectDataDirectory, "permafailed.json");
			if (File.Exists(failedFile))
			{
				HashSet<KeyType> m_permanentlyFailed = new HashSet<KeyType>();

				string[] failed = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(failedFile, Encoding.UTF8));
				foreach (string failedString in failed)
				{
					KeyType failedKey = StringToKey(failedString);
					m_permanentlyFailed.Add(failedKey);
				}

				foreach (KeyType itemKey in m_permanentlyFailed)
				{
					m_itemStatus[itemKey] = BatchItemStatus.PermanentlySkipped;
				}

				File.Move(failedFile, Path.Combine(Path.GetDirectoryName(failedFile), "permafailed_old.json"));
			}
		}
	}

	public override void SaveOut()
	{
		if (GetPersistStatus())
		{
			string statusFile = Path.Combine(ProjectDataDirectory, "status.json");
			File.WriteAllText(statusFile, JsonConvert.SerializeObject(m_itemStatus, Formatting.Indented));
		}

		base.SaveOut();
	}

	protected virtual KeyType StringToKey(string str)
	{
		return (KeyType)Convert.ChangeType(str, typeof(KeyType));
	}

	public virtual string GetImageCacheFilename(KeyType key, Dictionary<string, string> metadata)
	{
		return Path.Combine(ImageCacheDirectory, key + ".jpg");
	}

	public virtual string GetImageCroppedFilename(KeyType key, Dictionary<string, string> metadata)
	{
		return Path.Combine(ImageCacheDirectory, key + "_cropped.jpg");
	}

	public virtual string GetMetadataCacheFilename(KeyType key)
	{
		return Path.Combine(MetadataCacheDirectory, key + ".json");
	}

	public virtual string GetMetadataTrashFilename(KeyType key)
	{
		return Path.Combine(MetadataTrashDirectory, key + ".json");
	}

	public IEnumerable<KeyType> GetItemsWithStatus(BatchItemStatus status)
	{
		return m_itemStatus.Where(kv => kv.Value == status).Select(kv => kv.Key);
	}

	public BatchItemStatus GetItemStatus(KeyType key)
	{
		if (m_itemStatus.TryGetValue(key, out BatchItemStatus status))
		{
			return status;
		}
		else
		{
			return BatchItemStatus.Unknown;
		}
	}

	protected void RefreshHeartbeatData()
	{
		int[] counts = new int[(int)BatchItemStatus.COUNT];
		int total = 0;
		foreach (var kv in m_itemStatus)
		{
			if (kv.Value != BatchItemStatus.PermanentlySkipped)
			{
				total++;
			}
			counts[(int)kv.Value]++;
		}
		lock (Heartbeat)
		{
			Heartbeat.nTotal		= total;
			Heartbeat.nCompleted	= counts[(int)BatchItemStatus.Succeeded];
			Heartbeat.nDownloaded	= counts[(int)BatchItemStatus.Downloaded];
			Heartbeat.nFailed		= counts[(int)BatchItemStatus.Failed];
			Heartbeat.nFailedLicense = counts[(int)BatchItemStatus.LicenseFailed];
			Heartbeat.nDeclined		= counts[(int)BatchItemStatus.UploadDeclined];
		}
	}
}
