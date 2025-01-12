using System;
using System.IO;
using System.Net;
using System.Text;

namespace Tasks
{
	public class SingleUpload : BaseTask
	{
		public override void Execute()
		{
			//Download
			if (!Directory.Exists("images"))
				Directory.CreateDirectory("images");
			//foreach (string s in Directory.GetFiles("images"))
			//    File.Delete(s);

			string[] files = Directory.GetFiles("queue");

			Console.WriteLine("Logging in...");
			MediaWiki.Api Api = new MediaWiki.Api(new Uri("https://commons.wikimedia.org/"));
			Api.AutoLogIn();

			while (files.Length > 0)
			{
				StreamReader reader = new StreamReader(files[0], Encoding.Default);
				string path = reader.ReadLine();
				string title = reader.ReadLine();
				string content = reader.ReadToEnd();
				reader.Close();

				if (path.StartsWith("file://"))
				{
					path = path.Substring(7);
				}
				else
				{
					Console.WriteLine("Downloading");
					WebClient client = new WebClient();
					string newpath = Path.Combine("images", "tempA" + Path.GetExtension(path));
					client.DownloadFile(path, newpath);
					path = newpath;
				}

				//Upload
				Console.WriteLine(path);
				MediaWiki.Article art = new MediaWiki.Article();
				art.title = title;
				art.revisions = new MediaWiki.Revision[1];
				art.revisions[0] = new MediaWiki.Revision();
				art.revisions[0].text = content;
				Api.UploadFromLocal(art, path, "", false);

				File.Delete(files[0]);
				files = Directory.GetFiles("queue");
			}
		}
	}
}
