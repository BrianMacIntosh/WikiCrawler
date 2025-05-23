using MediaWiki;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace Tasks.Commons
{
	/// <summary>
	/// Uploads a simple list of files.
	/// </summary>
	public class SimpleUpload : BaseTask
	{
		public override void Execute()
		{
			//Download
			if (!Directory.Exists("images"))
				Directory.CreateDirectory("images");
			//foreach (string s in Directory.GetFiles("images"))
			//    File.Delete(s);

			string[] files = Directory.GetFiles("queue");

			Api Api = GlobalAPIs.Commons;

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
				Article art = new Article();
				art.title = PageTitle.Parse(title);
				art.title.Namespace = PageTitle.NS_File;
				art.revisions = new Revision[1];
				art.revisions[0] = new Revision();
				art.revisions[0].text = content;
				Api.UploadFromLocal(art, path, "", false);

				File.Delete(files[0]);
				files = Directory.GetFiles("queue");
			}
		}
	}
}
