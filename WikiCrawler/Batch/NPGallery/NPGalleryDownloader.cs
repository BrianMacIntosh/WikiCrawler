using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	public class NPGalleryDownloader : BatchDownloader
	{
		private HashSet<string> m_crawledAlbums = new HashSet<string>();

		public NPGalleryDownloader(string key)
			: base(key)
		{

		}

		private const string MetadataUriFormat = "https://npgallery.nps.gov/AssetDetail/{0}";

		protected override Uri GetItemUri(string key)
		{
			return new Uri(string.Format(MetadataUriFormat, key));
		}

		protected override IEnumerable<string> GetKeys()
		{
			yield return "e107f248-4b4e-410b-b93f-8ffc4cc21a6d";
			yield return "1FD0604F-1DD8-B71C-073AF79D82F66B9E";
		}

		protected override Dictionary<string, string> ParseMetadata(string pageContent)
		{
			const string keyTagOpen = "<label class=\"col-md-3 col-sm-3 text-right\">";
			const string keyTagClose = "</label>";
			const string valueTagOpen = "<div class=\"col-md-7 col-sm-7 text-left\">";
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
				keyStartIndex += keyTagOpen.Length;
				int keyEndIndex = pageContent.IndexOf(keyTagClose, keyStartIndex); //actually 1 past the end
				string key = pageContent.Substring(keyStartIndex, keyEndIndex - keyStartIndex).TrimEnd(':');

				int valueStartIndex = pageContent.IndexOf(valueTagOpen, keyEndIndex);
				if (valueStartIndex < 0)
				{
					throw new UWashException("key '" + key + "' had no value");
				}
				valueStartIndex += valueTagOpen.Length;
				int valueEndIndex = pageContent.IndexOf(valueTagClose, valueStartIndex); //actually 1 past the end
				string value = pageContent.Substring(valueStartIndex, valueEndIndex - valueStartIndex);

				metadata[key] = value.Trim();
				readHead = valueEndIndex;

			} while (true);

			return metadata;
		}
	}
}
