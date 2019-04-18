using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPGallery
{
	public class NPGalleryAsset
	{
		public string AssetID;
		public string AssetType;
		public string PrimaryType;
	}

	public class NPGallerySearchResult
	{
		public NPGalleryAsset Asset;
		public int ItemNumber;
	}

	public class NPGallerySearchResults
	{
		public NPGallerySearchResult[] Results;
	}
}
