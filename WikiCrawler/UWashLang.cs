using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WikiCrawler
{
	static class UWashLang
	{
		private const int LANG_QTY = 1000;

		public static void Do()
		{
			string[] files = Directory.GetFiles("uwash_cache");

			List<Dictionary<string, string>> metadata = new List<Dictionary<string, string>>();

			//find the appropriate quantity of metadata that hasn't been checked yet
			int stillLookingFor = LANG_QTY;
			int currentFile = 0;
			while (stillLookingFor > 0 && currentFile < files.Length)
			{
				Dictionary<string, string> myData = new Dictionary<string, string>();
				using (StreamReader reader = new StreamReader(new FileStream(files[currentFile], FileMode.Open), Encoding.Default))
				{
					while (!reader.EndOfStream)
					{
						myData[reader.ReadLine()] = reader.ReadLine();
					}
				}

				//found one
				if (!myData.ContainsKey("Language") && myData.ContainsKey("Caption"))
				{
					myData["Filename"] = files[currentFile];
					metadata.Add(myData);
					stillLookingFor--;
				}

				currentFile++;
			}

			//build query strings
			string[] queries = new string[metadata.Count];
			int i = 0;
			foreach (Dictionary<string, string> myData in metadata)
			{
				string query = "";
				if (myData.ContainsKey("Caption"))
					query += myData["Caption"] + ". ";
				if (myData.ContainsKey("Image Source Author"))
					query += myData["Image Source Author"] + ". ";
				if (myData.ContainsKey("Image Source Title"))
					query += myData["Image Source Title"] + ". ";
				queries[i] = query;
				i++;
			}

			//send
			string[] languages = DetectLanguage.Detect(queries);

			Console.WriteLine("Got " + languages.Length + " languages.");

			//insert results into metadata
			for (int c = 0; c < languages.Length; c++)
			{
				metadata[c]["Language"] = languages[c];
			}

			//resave all metadata
			foreach (Dictionary<string, string> myData in metadata)
			{
				using (StreamWriter writer = new StreamWriter(new FileStream(myData["Filename"], FileMode.Create), Encoding.Default))
				{
					foreach (KeyValuePair<string, string> kv in myData)
					{
						if (kv.Key != "Filename")
						{
							writer.WriteLine(kv.Key);
							writer.WriteLine(kv.Value);
						}
					}
				}
			}
		}
	}
}
