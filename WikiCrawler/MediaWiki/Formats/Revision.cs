using System.Collections.Generic;

namespace MediaWiki
{
	public class Revision
	{
		public int revid;
		public int parentid;
		public string contentformat;
		public string contentmodel;
		public string user;
		public string text;

		public Revision()
		{

		}

		public Revision(Dictionary<string, object> json)
		{
			object value;

			if (json.TryGetValue("revid", out value))
			{
				revid = (int)value;
			}
			if (json.TryGetValue("parentid", out value))
			{
				parentid = (int)value;
			}
			if (json.TryGetValue("contentformat", out value))
			{
				contentformat = (string)value;
			}
			if (json.TryGetValue("contentmodel", out value))
			{
				contentmodel = (string)value;
			}
			if (json.TryGetValue("user", out value))
			{
				user = (string)value;
			}
			if (json.TryGetValue("*", out value))
			{
				text = (string)value;
			}
		}
	}
}
