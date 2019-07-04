﻿using System.Runtime.Serialization;

/// <summary>
/// JSON deserialization target for a project configuration file.
/// </summary>
public class ProjectConfig
{
	public string downloader = "UWash";
	public string uploader = "UWash";

	public string projectPage = "";
	public string displayName = "";
	public string informationTemplate = "Photograph";
	public string sourceTemplate;
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
	public bool randomizeOrder = false;
	public bool manualApproval = false;

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

		if (string.IsNullOrEmpty(displayName))
		{
			if (downloader == "UWash")
			{
				displayName = "<a href=\"https://commons.wikimedia.org/wiki/Commons:Batch_uploading/University_of_Washington_Digital_Collections\">UWash</a> - " + collectionName;
			}
		}
		if (string.IsNullOrEmpty(projectPage))
		{
			if (downloader == "UWash")
			{
				projectPage = "Commons:Batch uploading/University of Washington Digital Collections";
			}
		}
	}
}
