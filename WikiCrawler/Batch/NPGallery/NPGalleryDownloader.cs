using System;
using System.Collections.Generic;
using System.IO;

namespace NPGallery
{
	public class NPGalleryDownloader : BatchDownloader
	{
		private HashSet<string> m_crawledAlbums = new HashSet<string>();

		private List<NPGalleryAsset> m_allAssets = new List<NPGalleryAsset>();

		public const int Version = 3;

		public NPGalleryDownloader(string key)
			: base(key)
		{
			EasyWeb.SetDelayForDomain(new Uri(MetadataUriFormat), 6f);

			// load existing assetlist
			string assetlistFile = Path.Combine(ProjectDataDirectory, "assetlist.json");
			string json = File.ReadAllText(assetlistFile);
			m_allAssets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NPGalleryAsset>>(json);
		}

		private const string MetadataUriFormat = "https://npgallery.nps.gov/AssetDetail/{0}";

		protected override Uri GetItemUri(string key)
		{
			return new Uri(string.Format(MetadataUriFormat, key));
		}

		protected override IEnumerable<string> GetKeys()
		{
			foreach (NPGalleryAsset asset in m_allAssets)
			{
				if (asset.AssetType == "Standard")
				{
					yield return asset.AssetID;
				}
			}
		}

		protected override Dictionary<string, string> ParseMetadata(string pageContent)
		{
			const string keyTagOpen = "<label class=\"col-md-3 col-sm-3"; // text-right\">
			const string keyTagClose = "</label>";
			const string valueTagOpen = "<div class=\"col-md-7 col-sm-7"; // text-left\">
			const string valueTagClose = "</div>";

			int readHead = pageContent.IndexOf("<!-- Metadata (Middle) Section -->");

			Dictionary<string, string> metadata = new Dictionary<string, string>();
			do
			{
				int keyStartIndex = pageContent.IndexOf(keyTagOpen, readHead);
				if (keyStartIndex < 0)
				{
					break;
				}
				keyStartIndex = pageContent.IndexOf('>', keyStartIndex) + 1;
				int keyEndIndex = pageContent.IndexOf(keyTagClose, keyStartIndex); //actually 1 past the end
				string key = pageContent.Substring(keyStartIndex, keyEndIndex - keyStartIndex).TrimEnd(':');

				int valueStartIndex = pageContent.IndexOf(valueTagOpen, keyEndIndex);
				if (valueStartIndex < 0)
				{
					throw new UWashException("key '" + key + "' had no value");
				}
				valueStartIndex = pageContent.IndexOf('>', valueStartIndex) + 1;
				int valueEndIndex = pageContent.IndexOf(valueTagClose, valueStartIndex); //actually 1 past the end
				string value = pageContent.Substring(valueStartIndex, valueEndIndex - valueStartIndex);

				metadata[key] = value.Trim();
				readHead = valueEndIndex;

			} while (true);

			// get albums
			int groupsIndex = pageContent.IndexOf("<!-- Groups Section -->", readHead);
			string related = "";
			if (groupsIndex >= 0)
			{
				string albumStartString = "<a href=\"/SearchResults/albumid/";
				string assetStartString = "<a href=\"/AssetDetail/";
				string preTitleString = "<p style=\"font-size:small\" data-toggle=\"tooltip\" data-placement=\"left\" title=\"";
				int searchIndex = groupsIndex;
				while (true)
				{
					int albumIndex = pageContent.IndexOf(albumStartString, searchIndex + 1);
					int assetIndex = pageContent.IndexOf(assetStartString, searchIndex + 1);
					string assetType;
					if (albumIndex >= 0 && (assetIndex < 0 || albumIndex < assetIndex))
					{
						assetType = "Album";
						searchIndex = albumIndex + albumStartString.Length;
					}
					else if (assetIndex >= 0)
					{
						assetType = "Asset";
						searchIndex = assetIndex + assetStartString.Length;
					}
					else
					{
						break;
					}
					int idEnd = pageContent.IndexOf('?', searchIndex);
					string assetId = pageContent.Substring(searchIndex, idEnd - searchIndex);
					searchIndex = idEnd;
					searchIndex = pageContent.IndexOf(preTitleString, searchIndex + 1);
					if (searchIndex < 0)
					{
						break;
					}
					int groupEndIndex = pageContent.IndexOf('"', searchIndex + preTitleString.Length);
					int albumStartIndex = searchIndex + preTitleString.Length;
					string title = pageContent.Substring(albumStartIndex, groupEndIndex - albumStartIndex);
					related = StringUtility.Join("|", related, assetId + "|" + assetType + "|" + title);
				}
			}
			metadata["~Related"] = related;
			metadata["~Version"] = Version.ToString();

			return metadata;
		}
	}
}
