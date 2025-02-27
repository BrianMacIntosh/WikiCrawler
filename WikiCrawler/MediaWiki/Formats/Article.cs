using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace MediaWiki
{
	/// <summary>
	/// Object representing a page on a MediaWiki instance.
	/// </summary>
	public class Article : Object
	{
		/// <summary>
		/// A list of changes that have been made to this article.
		/// </summary>
		public List<string> Changes = new List<string>();
		public bool Dirty = false;

		public string touched;
		public int counter;
		public int length;
		public string starttimestamp;
		public string edittoken;
		public Revision[] revisions;
		public Article[] links;
		public Article[] categories;

		public string imagerepository;
		public ImageInfo[] imageinfo;

		public Article()
		{

		}

		public Article(string title)
		{
			this.title = title;
		}

		public Article(Dictionary<string, object> json)
			: base(json)
		{
			if (json.ContainsKey("starttimestamp"))
				starttimestamp = (string)json["starttimestamp"];
			if (json.ContainsKey("edittoken"))
				edittoken = (string)json["edittoken"];
			if (!missing)
			{
				if (json.ContainsKey("touched"))
					touched = (string)json["touched"];
				if (json.ContainsKey("length"))
					length = (int)json["length"];
				if (json.ContainsKey("revisions"))
				{
					object[] revisionsJson = (object[])(json["revisions"]);
					revisions = new Revision[revisionsJson.Length];
					for (int c = 0; c < revisions.Length; c++)
					{
						Dictionary<string, object> revJson = (Dictionary<string, object>)revisionsJson[c];
						revisions[c] = new Revision(revJson);
					}
				}
				if (json.ContainsKey("links"))
				{
					links = ReadArticleArray(json, "links");
				}
				if (json.ContainsKey("categories"))
				{
					categories = ReadArticleArray(json, "categories");
				}
				if (json.ContainsKey("imagerepository"))
				{
					imagerepository = (string)json["imagerepository"];
				}
				if (json.ContainsKey("imageinfo"))
				{
					object[] imageinfoJson = (object[])(json["imageinfo"]);
					imageinfo = new ImageInfo[imageinfoJson.Length];
					for (int c = 0; c < imageinfo.Length; c++)
					{
						Dictionary<string, object> infoJson = (Dictionary<string, object>)imageinfoJson[c];
						imageinfo[c] = new ImageInfo(infoJson);
					}
				}
			}
		}

		private static Article[] ReadArticleArray(Dictionary<string, object> json, string key)
		{
			object[] articlesJson = (object[])(json[key]);
			Article[] articles = new Article[articlesJson.Length];
			for (int c = 0; c < articles.Length; c++)
			{
				Dictionary<string, object> revJson = (Dictionary<string, object>)articlesJson[c];
				articles[c] = new Article(revJson);
			}
			return articles;
		}

		/// <summary>
		/// Returns an enumerator over all pages that link to this one.
		/// </summary>
		public IEnumerable<Article> GetLinksHere(Api api)
		{
			//Download stream
			string basedata = "format=json" +
				"&action=query" +
				"&titles=" + Api.UrlEncode(title) +
				"&prop=linkshere" +
				"&lhlimit=5000";

			string data = basedata + "&continue=";

			bool lhcontinue = false;

			do
			{
				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(api.CreateApiRequest, data)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				Dictionary<string, object> query = (Dictionary<string, object>)deser["query"];
				Dictionary<string, object> pages = (Dictionary<string, object>)query["pages"];
				foreach (KeyValuePair<string, object> page in pages)
				{
					Dictionary<string, object> jsonData = (Dictionary<string, object>)page.Value;
					if (jsonData.ContainsKey("invalid"))
					{
						continue;
					}
					foreach (Dictionary<string, object> linkhere in (object[])jsonData["linkshere"])
					{
						yield return new Article(linkhere);
					}
				}

				lhcontinue = deser.ContainsKey("continue");
				if (lhcontinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					data = basedata;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						data += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (lhcontinue);
		}

		public static bool IsNullOrMissing(Article article)
		{
			return article == null || article.missing;
		}

		public static bool IsNullOrEmpty(Article article)
		{
			return article == null || article.missing || article.revisions == null
				|| article.revisions.Length == 0;
		}

		public string GetEditSummary()
		{
			StringBuilder summary = new StringBuilder();
			for (int c = 0; c < Changes.Count; c++)
			{
				summary.Append(Changes[c]);
				if (c < Changes.Count - 1)
				{
					summary.Append(", ");
				}
			}
			return summary.ToString();
		}
	}
}
