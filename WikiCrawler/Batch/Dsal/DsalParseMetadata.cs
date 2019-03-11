using Newtonsoft.Json;
using System.IO;

namespace Dsal
{
	public class DsalParseMetadata : BatchTask
	{
		private class Metadata
		{
			public string ImageUrl;
			public string Title;
			public string Photographer;
			public string Date;
			public string Subject;
			public string Notes;
			public string Location;
			public string OriginalNegativeHeld;
		}

		public DsalParseMetadata(string key)
			: base(key)
		{
		}

		public void Parse()
		{
			string[] lines = File.ReadAllLines(Path.Combine(ProjectDataDirectory, "metadata.txt"));
			for (int i = 9; i < lines.Length; i += 9)
			{
				Metadata meta = new Metadata();
				meta.ImageUrl = lines[i + 0];
				meta.Title = lines[i + 1];
				meta.Photographer = lines[i + 2];
				meta.Date = lines[i + 3];
				meta.Subject = lines[i + 4];
				meta.Notes = lines[i + 5];
				meta.Location = lines[i + 6];
				meta.OriginalNegativeHeld = lines[i + 7];
				string key = Path.GetFileNameWithoutExtension(meta.ImageUrl);
				File.WriteAllText(Path.Combine(MetadataCacheDirectory, key + ".json"), JsonConvert.SerializeObject(meta, Formatting.Indented));
			}
		}
	}
}
