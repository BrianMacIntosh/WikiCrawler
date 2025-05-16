using System;
using System.Collections.Generic;

namespace MediaWiki
{
	public struct InterwikiLink
	{
		public string prefix;
		public string value;

		public InterwikiLink(Dictionary<string, object> json)
		{
			if (json.ContainsKey("prefix"))
			{
				prefix = (string)json["prefix"];
			}
			else
			{
				prefix = string.Empty;
			}

			if (json.ContainsKey("*"))
			{
				value = (string)json["*"];
			}
			else
			{
				value = string.Empty;
			}
		}
	}

	public class Object
	{
		public long pageid;
		public Namespace ns;
		public string title;
		public long lastrevid;
		public InterwikiLink[] iwlinks;
		public Dictionary<string, string> pageprops;
		public bool missing = false;

		public Dictionary<string, object> raw;

		public Object()
		{

		}

		public Object(Dictionary<string, object> json)
		{
			raw = json;
			if (json.ContainsKey("ns"))
			{
				ns = (Namespace)json["ns"];
			}
			if (json.ContainsKey("title"))
			{
				title = (string)json["title"];
			}
			if (json.ContainsKey("missing"))
			{
				missing = true;
			}
			if (json.ContainsKey("pageid"))
			{
				pageid = Convert.ToInt64(json["pageid"]);
			}
			if (json.ContainsKey("lastrevid"))
			{
				lastrevid = Convert.ToInt64(json["lastrevid"]);
			}
			if (json.ContainsKey("iwlinks"))
			{
				iwlinks = ReadInterwikiLinkArray(json, "iwlinks");
			}
			if (json.ContainsKey("pageprops"))
			{
				pageprops = ReadPageProps(json, "pageprops");
			}
		}

		private static InterwikiLink[] ReadInterwikiLinkArray(Dictionary<string, object> json, string key)
		{
			object[] iwlinksJson = (object[])(json[key]);
			InterwikiLink[] iwlinks = new InterwikiLink[iwlinksJson.Length];
			for (int c = 0; c < iwlinks.Length; c++)
			{
				Dictionary<string, object> revJson = (Dictionary<string, object>)iwlinksJson[c];
				iwlinks[c] = new InterwikiLink(revJson);
			}
			return iwlinks;
		}

		private static Dictionary<string, string> ReadPageProps(Dictionary<string, object> json, string key)
		{
			Dictionary<string, string> pageprops = new Dictionary<string, string>();
			foreach (var kv in (Dictionary<string, object>)json[key])
			{
				pageprops.Add(kv.Key, (string)kv.Value);
			}
			return pageprops;
		}

		/// <summary>
		/// Returns the article title without the namespace.
		/// </summary>
		public string GetTitle()
		{
			int colon = title.IndexOf(':');
			if (colon > 0)
			{
				return title.Substring(colon + 1);
			}
			else
			{
				return title;
			}
		}
	}
}
