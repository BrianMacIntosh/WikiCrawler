using MediaWiki;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using Tasks.Commons;

namespace Tasks
{
	/// <summary>
	/// Populates the implicitcreators database from past edits.
	/// </summary>
	public class ImplicitCreatorsHistorical : BaseTask
	{
		public ImplicitCreatorsHistorical()
		{
			Parameters["StartTime"] = "2025-01-17 00:00:00";
			Parameters["EndTime"] = "2025-05-07 00:00:00";
			Parameters["User"] = "BMacZeroBot";
		}

		public override void Execute()
		{
			Api Api = GlobalAPIs.Commons;

			string startTime = Parameters["StartTime"];
			string endTime = Parameters["EndTime"];

			EasyWeb.SetDelayForDomain(Api.Domain, 0);

			SQLiteConnection database = ImplicitCreatorsReplacement.ConnectFilesDatabase(true);

			foreach (Contribution contrib in Api.GetContributions(Parameters["User"], endTime, startTime))
			{
				ConsoleUtility.WriteLine(ConsoleColor.White, contrib.title.FullTitle);

				if (ImplicitCreatorsReplacement.IsFileCached(database, contrib.title))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  Already cached.");
					continue;
				}

				if (contrib.comment.Contains("replace inline creator")
					|| contrib.comment.Contains("remove redundant creator lifespan")
					|| contrib.comment.Contains("replace implicit creator")) //TODO: check that this catches all
				{
					Article article = Api.GetPage(contrib.title, rvlimit: 2, rvstart: contrib.timestamp, rvdir: Direction.Older);

					// find the revision before the contrib
					CommonsFileWorksheet worksheet = new CommonsFileWorksheet(article, 1);
					CacheFile(database, contrib.title, worksheet.Author);
				}
			}
		}

		private void CacheFile(SQLiteConnection database, PageTitle title, string author)
		{
			SQLiteCommand command = database.CreateCommand();
			command.CommandText = "INSERT INTO files (pageTitle, authorString, replaced, touchTimeUnix) VALUES ($pageTitle, $authorString, true, unixepoch()) "
				+ "ON CONFLICT (pageTitle) DO UPDATE SET authorString=$authorString,touchTimeUnix=unixepoch()";
			command.Parameters.AddWithValue("pageTitle", title);
			command.Parameters.AddWithValue("authorString", author);
			Debug.Assert(command.ExecuteNonQuery() == 1);
		}
	}
}
