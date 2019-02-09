using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	public class NPGalleryUploader : BatchUploader
	{
		public NPGalleryUploader(string key)
			: base(key)
		{

		}

		private const string ImageUriFormat = "https://npgallery.nps.gov/GetAsset/{0}/original.jpg";

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			throw new NotImplementedException();
		}

		protected override Uri GetImageUri(string key)
		{
			return new Uri(string.Format(ImageUriFormat, key));
		}

		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			throw new NotImplementedException();
		}

		protected override string GetUploadImagePath(string key, Dictionary<string, string> metadata)
		{
			throw new NotImplementedException();
		}
	}
}
