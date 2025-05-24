using System;
using System.Collections.Generic;
using System.IO;
using Tasks;

namespace NPGallery
{
	public class NPAssets : BaseTask
	{
		public override void Execute()
		{
			new NPGalleryAssetListDownloader("npgallery").Execute();
		}
	}

	public class NPGalleryAssetListDownloader : BatchDownloader<int>
	{
		private List<NPGalleryAsset> m_allAssets = new List<NPGalleryAsset>();

		private const string MetadataUriFormat = "https://npgallery.nps.gov/SearchResults/f51d9701-865e-4950-b316-ff1567e7330f?page={0}&showfilters=false&filterUnitssc=10&view=grid&sort=date-desc";

		public NPGalleryAssetListDownloader(string key)
			: base(key)
		{
			// load existing assetlist
			string assetlistFile = Path.Combine(ProjectDataDirectory, "assetlist.json");
			string json = File.ReadAllText(assetlistFile);
			m_allAssets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NPGalleryAsset>>(json);
		}

		protected override HashSet<int> GetDownloadSucceededKeys()
		{
			return new HashSet<int>();
		}

		protected override bool GetPersistStatus()
		{
			return false;
		}

		protected override void SaveOut()
		{
			base.SaveOut();

			string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(m_allAssets, Newtonsoft.Json.Formatting.None);
			File.WriteAllText(Path.Combine(ProjectDataDirectory, "assetlist.json"), serialized);
		}

		protected override Uri GetItemUri(int key)
		{
			return new Uri(string.Format(MetadataUriFormat, key));
		}

		protected override IEnumerable<int> GetKeys()
		{
			for (int page = 1; page <= 9396; page++)
			{
				yield return page;
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

				int Dupes = 0;
				foreach (NPGallerySearchResult result in results.Results)
				{
					if (m_allAssets.Contains(result.Asset))
					{
						Dupes++;
					}
					else
					{
						m_allAssets.Add(result.Asset);
					}
				}

				int DupePct = (int)Math.Round(100f * Dupes / results.Results.Length);
				Console.WriteLine(string.Format("Dupes: {0}/{1} ({2}%)", Dupes, results.Results.Length, DupePct));
			}
			else
			{
				throw new Exception();
			}

			return null;
		}
	}
}
