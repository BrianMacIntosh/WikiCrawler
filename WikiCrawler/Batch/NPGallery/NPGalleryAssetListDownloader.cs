﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NPGallery
{
	public class NPGalleryAssetListDownloader : BatchDownloader
	{
		private List<NPGalleryAsset> m_allAssets = new List<NPGalleryAsset>();

		private const string MetadataUriFormat = "https://npgallery.nps.gov/SearchResults/86bf834852ce4c7a881bf76ae04a2f47?page={0}&showfilters=false&filterUnitssc=10&view=grid&sort=date-desc";

		public NPGalleryAssetListDownloader(string key)
			: base(key)
		{
			EasyWeb.SetDelayForDomain(new Uri(MetadataUriFormat), 6f);

			// load existing assetlist
			string assetlistFile = Path.Combine(ProjectDataDirectory, "assetlist.json");
			string json = File.ReadAllText(assetlistFile);
			m_allAssets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NPGalleryAsset>>(json);
		}

		protected override void SaveOut()
		{
			base.SaveOut();

			string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(m_allAssets, Newtonsoft.Json.Formatting.None);
			File.WriteAllText(Path.Combine(ProjectDataDirectory, "assetlist.json"), serialized);
		}

		protected override Uri GetItemUri(string key)
		{
			return new Uri(string.Format(MetadataUriFormat, key));
		}

		protected override IEnumerable<string> GetKeys()
		{
			for (int page = 1; page <= 6712; page++)
			{
				yield return page.ToString();
			}
		}
		
		protected override Dictionary<string, string> ParseMetadata(string pageContent)
		{
			// instead, grab the IDs of the images
			int javascriptStart = pageContent.IndexOf("var search = {");

			if (javascriptStart >= 0)
			{
				int javascriptEnd = pageContent.IndexOf("};", javascriptStart);

				int jsonStart = javascriptStart + "var search = {".Length - 2;
				string jsonText = pageContent.Substring(jsonStart, javascriptEnd - jsonStart + 1);
				NPGallerySearchResults results = Newtonsoft.Json.JsonConvert.DeserializeObject<NPGallerySearchResults>(jsonText);

				foreach (NPGallerySearchResult result in results.Results)
				{
					if (m_allAssets.Contains(result.Asset))
					{
						// we are up-to-date
						Finished = true;
					}
					else
					{
						m_allAssets.Add(result.Asset);
					}
				}
			}
			else
			{
				throw new Exception();
			}

			return null;
		}
	}
}
