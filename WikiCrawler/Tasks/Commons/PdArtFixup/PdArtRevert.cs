using MediaWiki;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace Tasks.Commons
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
			IEnumerable<PageTitle> files = File.ReadAllLines("E:/revert.csv", Encoding.UTF8)
				.Select(s => PageTitle.Parse(s.Trim('"').Replace("\"\"", "\"")))
				.Where(t => !t.IsEmpty);

			SQLiteConnection connection = PdArtReplacement.ConnectFilesDatabase(true);

			foreach (Article page in GlobalAPIs.Commons.GetPages(files.ToArray(), rvprop: "content|ids|user"))
			{
				Console.WriteLine(page.title);

				if (page.title.Name.Contains("?"))
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, "You dummy.");
				}
				else if (page.revisions[0].user == Parameters["User"])
				{
					ConsoleUtility.WriteLine(ConsoleColor.Green, "Undoing.");
					GlobalAPIs.Commons.UndoRevision((int)page.pageid, page.revisions[0].revid, "", true);

					SQLiteCommand command = connection.CreateCommand();
					command.CommandText = "UPDATE files SET replaced=0 WHERE pageTitle=$pageTitle";
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