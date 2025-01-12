using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Base class for a batch file upload or download task with uniquely-keyed files.
/// </summary>
/// <typeparam name="KeyType">The type of the unique key for files in this task.</typeparam>
public abstract class BatchTaskKeyed<KeyType> : BatchTask
{
	/// <summary>
	/// Set of keys for files that have already been uploaded.
	/// </summary>
	protected HashSet<KeyType> m_succeeded = new HashSet<KeyType>();
	protected HashSet<KeyType> m_permanentlyFailed = new HashSet<KeyType>();

	public BatchTaskKeyed(string key)
		: base(key)
	{
		// load already-succeeded uploads
		if (GetPersistStatus())
		{
			string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
			if (File.Exists(succeededFile))
			{
				string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(succeededFile, Encoding.UTF8));
				foreach (string succeededString in succeeded)
				{
					KeyType succeededKey = StringToKey(succeededString);
					m_succeeded.Add(succeededKey);
				}
			}

			string failedFile = Path.Combine(ProjectDataDirectory, "permafailed.json");
			if (File.Exists(failedFile))
			{
				string[] failed = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(failedFile, Encoding.UTF8));
				foreach (string failedString in failed)
				{
					KeyType failedKey = StringToKey(failedString);
					m_permanentlyFailed.Add(failedKey);
				}
			}
		}
	}

	protected override void SaveOut()
	{
		if (GetPersistStatus())
		{
			string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
			List<KeyType> succeeded = m_succeeded.ToList();
			succeeded.Sort();
			File.WriteAllText(succeededFile, JsonConvert.SerializeObject(succeeded, Formatting.Indented));

			string permafailedFile = Path.Combine(ProjectDataDirectory, "permafailed.json");
			List<KeyType> failed = m_permanentlyFailed.ToList();
			failed.Sort();
			File.WriteAllText(permafailedFile, JsonConvert.SerializeObject(failed, Formatting.Indented));
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

}
