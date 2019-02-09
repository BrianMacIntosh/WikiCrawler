using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	public class NPGalleryAlbumDownloader : BatchDownloader
	{
		private const string MetadataUriFormat = "https://npgallery.nps.gov/SearchResults/albumid/{0}";

		public NPGalleryAlbumDownloader(string key)
			: base(key)
		{

		}

		protected override Uri GetItemUri(string key)
		{
			return new Uri(string.Format(MetadataUriFormat, key));
		}

		protected override IEnumerable<string> GetKeys()
		{
			throw new NotImplementedException();
		}

		protected override Dictionary<string, string> ParseMetadata(string pageContent)
		{
			// instead, grab the IDs of the images
			throw new NotImplementedException();

			return null;
		}
	}
}
