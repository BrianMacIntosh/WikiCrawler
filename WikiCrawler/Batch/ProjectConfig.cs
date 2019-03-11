using System.Runtime.Serialization;

/// <summary>
/// JSON deserialization target for a project configuration file.
/// </summary>
public class ProjectConfig
{
	public string downloader = "UWash";
	public string uploader = "UWash";

	public string informationTemplate = "Photograph";
	public string defaultAuthor = "unknown";
	public string defaultPubCountry;

	/// <summary>
	/// Full English name of the collection.
	/// </summary>
	public string collectionName;

	/// <summary>
	/// (Optional) Override master category for media.
	/// </summary>
	public string masterCategory;

	/// <summary>
	/// (Optional) Override check category for media.
	/// </summary>
	public string checkCategory;

	public string[] additionalCategories;
	public string filenameSuffix;

	public bool allowFailedCreators = false;

	public bool allowUpload = true;
	public bool allowDataDownload = true;
	public bool allowImageDownload = true;

	public int maxFailed = int.MaxValue;
	public int maxNew = int.MaxValue;
	public int maxSuccesses = int.MaxValue;

	[OnDeserialized]
	internal void OnDeserializedMethod(StreamingContext context)
	{
		if (!string.IsNullOrEmpty(collectionName))
		{
			if (string.IsNullOrEmpty(masterCategory))
			{
				masterCategory = "Category:Images from the " + collectionName;
			}
			if (string.IsNullOrEmpty(checkCategory))
			{
				checkCategory = "Category:Images from the " + collectionName + " to check";
			}
		}
	}
}
