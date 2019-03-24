using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

	protected HashSet<string> m_succeeded = new HashSet<string>();

	protected string m_projectKey { get; private set; }
	protected ProjectConfig m_config { get; set; }

	public BatchTask(string key)
	{
		m_projectKey = key;
		m_config = JsonConvert.DeserializeObject<ProjectConfig>(
			File.ReadAllText(Path.Combine(ProjectDataDirectory, "config.json")));

		// load already-succeeded uploads
		string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
		if (File.Exists(succeededFile))
		{
			string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(succeededFile, Encoding.UTF8));
			foreach (string suc in succeeded)
			{
				m_succeeded.Add(suc);
			}
		}
	}

	protected virtual void SaveOut()
	{
		string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
		List<string> succeeded = m_succeeded.ToList();
		succeeded.Sort();
		File.WriteAllText(succeededFile, JsonConvert.SerializeObject(m_succeeded, Formatting.Indented));
	}
}
