using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
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
		private const string MetadataUriFormat = "https://npgallery.nps.gov/SearchResults/92380621-129d-44ad-997e-d05743a2b052?page={0}&showfilters=false&filterUnitssc=10&view=grid&sort=date-desc";

		public readonly SQLiteConnection AssetsDatabase;

		public NPGalleryAssetListDownloader(string key)
			: base(key)
		{
			SQLiteConnectionStringBuilder connectionString = new SQLiteConnectionStringBuilder
			{
				{ "Data Source", NPGallery.AssetDatabaseFile },
				{ "Mode", "ReadWrite" }
			};
			AssetsDatabase = new SQLiteConnection(connectionString.ConnectionString);
			AssetsDatabase.Open();
		}

		protected override HashSet<int> GetDownloadSucceededKeys()
		{
			return new HashSet<int>();
		}

		protected override bool GetPersistStatus()
		{
			return false;
		}

		protected override Uri GetItemUri(int key)
		{
			return new Uri(string.Format(MetadataUriFormat, key));
		}

		protected override IEnumerable<int> GetKeys()
		{
			for (int page = 1; page <= 10000; page++)
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

				SQLiteCommand command = AssetsDatabase.CreateCommand();
				command.CommandText = "INSERT INTO assets (id, assettype, primarytype) "
					+ "VALUES ($id, $assettype, $primarytype) ON CONFLICT(id) DO NOTHING;";

				int Dupes = 0;
				foreach (NPGallerySearchResult result in results.Results)
				{
					command.Parameters.AddWithValue("id", result.Asset.AssetID.Replace("-", ""));
					command.Parameters.AddWithValue("assettype", result.Asset.AssetType);
					command.Parameters.AddWithValue("primarytype", result.Asset.PrimaryType);
					if (command.ExecuteNonQuery() == 0)
					{
						Dupes++;
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
