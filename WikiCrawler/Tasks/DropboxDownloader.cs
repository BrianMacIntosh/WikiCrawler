﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace Tasks
{
	/// <summary>
	/// Given JSON data from a Dropbox folder, downloads all the files in that folder.
	/// </summary>
	public class DropboxDownloader : BaseTask
	{
		public override void Execute()
		{
			string json;

			using (StreamReader reader = new StreamReader(new FileStream("E:/dropbox.json", FileMode.Open, FileAccess.Read)))
			{
				json = reader.ReadToEnd();
			}

			JavaScriptSerializer serializer = new JavaScriptSerializer();
			serializer.MaxJsonLength = 5 * 1024 * 1024;
            Dictionary<string, object> deserialized = (Dictionary<string, object>)serializer.DeserializeObject(json);
			object[] components = (object[])deserialized["components"];

			WebClient client = new WebClient();

			Uri dropboxUri = new Uri("http://www.dropbox.com/");
			WebThrottle throttle = WebThrottle.Get(dropboxUri);
			throttle.CrawlDelay = 10f;
			
			for (int i = 0; i < components.Length; i++)
			{
				Dictionary<string, object> component = (Dictionary<string, object>)components[i];
				if (component.ContainsKey("props"))
				{
					Dictionary<string, object> props = (Dictionary<string, object>)component["props"];
					if (props.ContainsKey("contents"))
					{
						Dictionary<string, object> contents = (Dictionary<string, object>)props["contents"];
						if (contents.ContainsKey("files"))
						{
							object[] files = (object[])contents["files"];
							for (int j = 0; j < files.Length; j++)
							{
								Dictionary<string, object> file = (Dictionary<string, object>)files[j];
								string href = (string)file["href"];
								if (href.EndsWith("?dl=0"))
								{
									href = href.Substring(0, href.Length - "?dl=0".Length) + "?dl=1";
								}
								string filename = (string)file["filename"];
								Console.WriteLine(filename);

								string localPath = Path.Combine("E:/p3files", filename);
								if (File.Exists(localPath)) continue;

								Console.WriteLine("DOWNLOADING...");

								throttle.WaitForDelay();
								client.DownloadFile(href, localPath);
							}
						}
					}
				}
			}
		}
	}
}
