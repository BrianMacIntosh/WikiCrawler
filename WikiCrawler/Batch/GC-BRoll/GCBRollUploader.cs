using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 
/// </summary>
/// <remarks>Unfinished.</remarks>
public class GCBRollUploader : BatchUploader<string>
{
	public GCBRollUploader(string key)
		: base(key)
	{

	}

	public override Dictionary<string, string> LoadMetadata(string key, bool always = false)
	{
		string cacheFile = GetMetadataCacheFilename(key);
		string[] lines = File.ReadAllLines(cacheFile, Encoding.UTF8);

		Dictionary<string, string> metadata = new Dictionary<string, string>();
		char[] colon = new char[] { ':' };
		metadata.Add("Title", lines[0]);
		metadata.Add("Duration", lines[1].Split(colon, 2)[1]);
		metadata.Add("Date", lines[2].Split(colon, 2)[1]);
		metadata.Add("Credit", lines[3].Split(colon, 2)[1]);
		metadata.Add("Description", lines[6]);
		return metadata;
	}

	public override void CacheImage(string key, Dictionary<string, string> metadata)
	{
		//base.CacheImage(key, metadata);
	}

	protected override string BuildPage(string key, Dictionary<string, string> metadata)
	{
		string page = "=={{int:filedesc}}==\n"
				+ "{{Information\n"
				+ "|description={{en|1=" + metadata["Description"] + "}}\n"
				+ "|date=" + DateUtility.ParseDate(metadata["Date"], out DateParseMetadata dateParse) + "\n"
				+ "|source=[https://www.nps.gov/grca/learn/photosmultimedia/b-roll_hd_index.htm Grand Canyon B-Roll Video Index]\n"
				+ "|author=" + metadata["Credit"] + "\n"
				+ "|permission=\n"
				+ "|other_versions=\n"
				+ "|other_fields=\n"
				+ "}}\n"
				+ "\n"
				+ "=={{int:license-header}}==\n"
				+ "{{PD-USGov-NPS}}\n"
				+ "\n"
				+ "[[Category:Videos from Grand Canyon National Park]]\n";

		return page;
	}

	public override string GetImageCacheFilename(string key, Dictionary<string, string> metadata)
	{
		return Path.Combine(ImageCacheDirectory, key + ".mp4");
	}

	public override string GetMetadataCacheFilename(string key)
	{
		return Path.ChangeExtension(base.GetMetadataCacheFilename(key), ".txt");
	}

	public override Uri GetImageUri(string key, Dictionary<string, string> metadata)
	{
		return new Uri("");
	}

	public override PageTitle GetTitle(string key, Dictionary<string, string> metadata)
	{
		return new PageTitle(PageTitle.NS_File, "Grand Canyon B-Roll: " + metadata["Title"]);
	}
}
