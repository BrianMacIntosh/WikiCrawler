using System.Collections.Generic;

namespace MediaWiki
{
	public class Contribution
	{
		public string user;
		public int pageid;
		public int revid;
		public int ns;
		public PageTitle title;
		public string timestamp;
		public string comment;

		public Contribution()
		{

		}

		public Contribution(Dictionary<string, object> json)
		{
			object value;

			if (json.TryGetValue("user", out value))
			{
				user = (string)value;
			}
			if (json.TryGetValue("pageid", out value))
			{
				pageid = (int)value;
			}
			if (json.TryGetValue("revid", out value))
			{
				revid = (int)value;
			}
			if (json.TryGetValue("ns", out value))
			{
				ns = (int)value;
			}
			if (json.TryGetValue("title", out value))
			{
				title = PageTitle.Parse((string)value);
			}
			if (json.TryGetValue("timestamp", out value))
			{
				timestamp = (string)value;
			}
			if (json.TryGetValue("comment", out value))
			{
				comment = (string)value;
			}
		}
	}
}
