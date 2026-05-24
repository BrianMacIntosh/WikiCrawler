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
	private Dictionary<KeyType, BatchItemStatus> m_itemStatus = new Dictionary<KeyType, BatchItemStatus>();

	private int[] m_statusCounts = new int[(int)BatchItemStatus.COUNT];

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
				RebuildStatusCounts();
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

			RebuildStatusCounts();
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

	public void SetItemStatus(KeyType key, BatchItemStatus status)
	{
		if (m_itemStatus.TryGetValue(key, out BatchItemStatus oldStatus))
		{
			if (status == oldStatus) return;

			m_statusCounts[(int)oldStatus]--;
		}
		m_itemStatus[key] = status;
		m_statusCounts[(int)status]++;
		RefreshHeartbeatData();
	}

	protected void RebuildStatusCounts()
	{
		m_statusCounts.Fill(0);
		foreach (var kv in m_itemStatus)
		{
			m_statusCounts[(int)kv.Value]++;
		}
		RefreshHeartbeatData();
	}

	protected void RefreshHeartbeatData()
	{
		// count total
		int totalCount = m_statusCounts.Sum() - m_statusCounts[(int)BatchItemStatus.PermanentlySkipped];

		lock (Heartbeat)
		{
			Heartbeat.nTotal		= totalCount;
			Heartbeat.nCompleted	= m_statusCounts[(int)BatchItemStatus.Succeeded];
			Heartbeat.nDownloaded	= m_statusCounts[(int)BatchItemStatus.Downloaded];
			Heartbeat.nFailed		= m_statusCounts[(int)BatchItemStatus.Failed];
			Heartbeat.nFailedLicense = m_statusCounts[(int)BatchItemStatus.LicenseFailed];
			Heartbeat.nDeclined		= m_statusCounts[(int)BatchItemStatus.UploadDeclined];
		}
	}
}
