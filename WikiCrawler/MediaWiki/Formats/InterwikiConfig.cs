using System.Collections.Generic;

namespace MediaWiki
{
	public class InterwikiConfig
	{
		public string prefix;
		public string local;
		public string language;
		public string bcp47;

		/// <summary>
		/// Uses '$1' as a wildcard for the page name.
		/// </summary>
		public string url;

		public InterwikiConfig()
		{

		}

		public InterwikiConfig(Dictionary<string, object> json)
		{
			object value;

			if (json.TryGetValue("prefix", out value))
			{
				prefix = (string)value;
			}
			if (json.TryGetValue("local", out value))
			{
				local = (string)value;
			}
			if (json.TryGetValue("language", out value))
			{
				language = (string)value;
			}
			if (json.TryGetValue("bcp47", out value))
			{
				bcp47 = (string)value;
			}
			if (json.TryGetValue("url", out value))
			{
				url = (string)value;
			}
		}
	}
}
