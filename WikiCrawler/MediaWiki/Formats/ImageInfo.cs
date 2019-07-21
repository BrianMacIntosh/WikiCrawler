using System.Collections.Generic;

namespace MediaWiki
{
	public class ImageInfo
	{
		public string comment;
		public string url;
		public string descriptionurl;
		public string descriptionshorturl;

		public ImageInfo()
		{

		}

		public ImageInfo(Dictionary<string, object> json)
		{
			object value;

			if (json.TryGetValue("comment", out value))
			{
				comment = (string)value;
			}
			if (json.TryGetValue("url", out value))
			{
				url = (string)value;
			}
			if (json.TryGetValue("descriptionurl", out value))
			{
				descriptionurl = (string)value;
			}
			if (json.TryGetValue("descriptionshorturl", out value))
			{
				descriptionshorturl = (string)value;
			}
		}
	}
}
