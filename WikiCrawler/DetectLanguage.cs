using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;

namespace WikiCrawler
{
	static class DetectLanguage
	{
		//free limit is 5000/day

		private const string API_URL = "http://ws.detectlanguage.com/0.2/detect";
		private static readonly string API_KEY;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ApiKeyFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "key-detectlanguage.txt"); }
		}

		static DetectLanguage()
		{
			API_KEY = File.ReadAllText(ApiKeyFile).Trim();
		}

		private static HttpWebRequest CreateRequest()
		{
			return (HttpWebRequest)WebRequest.Create(API_URL);
		}

		public static string Detect(string text)
		{
			using (StreamReader reader = new StreamReader(
				WebInterface.HttpPost(CreateRequest, "q=" + HttpUtility.UrlEncode(text) + "&key=" + API_KEY)))
			{
				string json = reader.ReadToEnd();
				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				Dictionary<string, object> data = (Dictionary<string, object>)deser["data"];
				object[] detections = (object[])data["detections"];
				Dictionary<string, object> best = (Dictionary<string, object>)detections[0];
				return (string)best["language"];
			}
		}

		public static string[] Detect(string[] text)
		{
			string q = "";
			for (int c = 0; c < text.Length; c++)
			{
				q += "q[]=" + HttpUtility.UrlEncode(text[c]) + "&";
			}

			using (StreamReader reader = new StreamReader(
				//new FileStream("lang_response.json", FileMode.Open)))
				WebInterface.HttpPost(CreateRequest, q + "key=" + API_KEY)))
			{
				string json = reader.ReadToEnd();
				/*using (StreamWriter writer = new StreamWriter(new FileStream("lang_response.json", FileMode.Create)))
				{
					writer.Write(json);
				}*/

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				Dictionary<string, object> data = (Dictionary<string, object>)deser["data"];
				object[] detections = (object[])data["detections"];

				string[] result = new string[detections.Length];
				for (int c = 0; c < detections.Length; c++)
				{
					result[c] = (string)((Dictionary<string, object>)((object[])detections[c])[0])["language"];
				}
				return result;
			}
		}
	}
}
