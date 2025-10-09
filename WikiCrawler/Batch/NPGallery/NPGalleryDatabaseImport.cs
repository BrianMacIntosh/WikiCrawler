using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Tasks;

namespace NPGallery
{
	public class NPGalleryDatabaseImport : BaseTask
	{
		public readonly SQLiteConnection AssetsDatabase;

		public NPGalleryDatabaseImport()
		{
			SQLiteConnectionStringBuilder connectionString = new SQLiteConnectionStringBuilder
			{
				{ "Data Source", NPGallery.AssetDatabaseFile },
				{ "Mode", "ReadWrite" }
			};
			AssetsDatabase = new SQLiteConnection(connectionString.ConnectionString);
			AssetsDatabase.Open();
		}

		public override void Execute()
		{
			// load existing assetlist
			string assetlistFile = Path.Combine(NPGallery.ProjectDataDirectory, "assetlist.json");
			string json = File.ReadAllText(assetlistFile);
			List<NPGalleryAsset> externalAssets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NPGalleryAsset>>(json);

			SQLiteCommand command = AssetsDatabase.CreateCommand();
			command.CommandText = "INSERT INTO assets (id, assettype, primarytype) "
				+ "VALUES ($id, $assettype, $primarytype) ON CONFLICT(id) DO NOTHING;";
			foreach (NPGalleryAsset asset in externalAssets)
			{
				System.Console.WriteLine(asset.AssetID);
				command.Parameters.AddWithValue("id", asset.AssetID.Replace("-", ""));
				command.Parameters.AddWithValue("assettype", asset.AssetType);
				command.Parameters.AddWithValue("primarytype", asset.PrimaryType);
				command.ExecuteNonQuery();
			}
		}
	}
}
