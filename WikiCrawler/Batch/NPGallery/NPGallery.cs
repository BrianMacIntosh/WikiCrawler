using System;
using System.IO;
using WikiCrawler;

namespace NPGallery
{
	public static class NPGallery
	{
		public static Guid StringToKey(string str)
		{
			// the GUIDs from NPGallery are sometimes missing the last hyphen
			return new Guid(str.Replace("-", ""));
		}

		public static string ProjectDataDirectory => Path.Combine(Configuration.DataDirectory, "npgallery");

		public static string AssetDatabaseFile => Path.Combine(ProjectDataDirectory, "npgallery.db");
	}
}
