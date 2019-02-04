using System.IO;
using WikiCrawler;

public abstract class BatchTask
{
	protected string ImageCacheDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "images"); }
	}

	protected string MetadataCacheDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "data_cache"); }
	}

	protected string ProjectDataDirectory
	{
		get { return Path.Combine(Configuration.DataDirectory, m_projectKey); }
	}

	protected string GetImageUrl(string key)
	{
		return string.Format(ImageCacheDirectory, key);
	}

	protected string GetImageCacheFilename(string key)
	{
		return Path.Combine(ImageCacheDirectory, key + ".jpg");
	}

	protected string GetImageCroppedFilename(string key)
	{
		return Path.Combine(ImageCacheDirectory, key + "_cropped.jpg");
	}

	protected string GetMetadataCacheFilename(string key)
	{
		return Path.Combine(MetadataCacheDirectory, key + ".json");
	}

	protected string m_projectKey { get; private set; }
	protected ProjectConfig m_config { get; private set; }

	public BatchTask(string key, ProjectConfig config)
	{
		m_projectKey = key;
		m_config = config;
	}
}
