using MediaWiki;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace Tasks
{
	/// <summary>
	/// 
	/// </summary>
	public class PdArtRevert : BaseTask
	{
		public PdArtRevert()
		{
			Parameters.Add("User", "BMacZeroBot");
		}

		public override void Execute()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			Api Api = new Api(commons);
			Api.AutoLogIn();

			IEnumerable<string> files = File.ReadAllLines("E:/revert.csv", Encoding.UTF8)
				.Select(s => s.Trim('"').Replace("\"\"", "\""))
				.Where(s => !string.IsNullOrWhiteSpace(s));

			SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(true);

			foreach (Article page in Api.GetPages(files.ToArray(), rvprop: "content|ids|user"))
			{
				Console.WriteLine(page.title);

				if (page.title.Contains("?"))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "You dummy.");
				}
				else if (page.revisions[0].user == Parameters["User"])
				{
					ConsoleUtility.WriteLine(ConsoleColor.Green, "Undoing.");
					Api.UndoRevision(page.pageid, page.revisions[0].revid, "", true);

					SQLiteCommand command = connection.CreateCommand();
					command.CommandText = "UPDATE files SET bLicenseReplaced=0 WHERE pageTitle=$pageTitle";
					command.Parameters.AddWithValue("pageTitle", page.title);
					int result = command.ExecuteNonQuery();
					Console.WriteLine(result);
				}
				else
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "Last editor mismatch.");
				}
			}
		}
	}
}