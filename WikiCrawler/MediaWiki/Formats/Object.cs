using System.Collections.Generic;

namespace MediaWiki
{
	public class Object
	{
		public int pageid;
		public Namespace ns;
		public string title;
		public int lastrevid;
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
				missing = json.ContainsKey("missing");
			}
			if (!missing)
			{
				pageid = (int)json["pageid"];
				if (json.ContainsKey("lastrevid"))
					lastrevid = (int)json["lastrevid"];
			}
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
