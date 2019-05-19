using System;

namespace NPGallery
{
	public class NPGalleryAsset : IEquatable<NPGalleryAsset>
	{
		public string AssetID;
		public string AssetType;
		public string PrimaryType;

		public bool Equals(NPGalleryAsset other)
		{
			return other.AssetID == AssetID;
		}

		public override int GetHashCode()
		{
			return AssetID.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			NPGalleryAsset otherAsset = obj as NPGalleryAsset;
			return otherAsset != null && Equals(otherAsset);
		}
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
