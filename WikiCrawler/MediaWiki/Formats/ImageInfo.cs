using System.Collections.Generic;

namespace MediaWiki
{
	public class ImageInfo
	{
		public string comment;
		public string url;
		public string descriptionurl;
		public string descriptionshorturl;
		public Dictionary<string, string> metadata;

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
			if (json.TryGetValue("metadata", out value))
			{
				metadata = new Dictionary<string, string>();
				foreach (object item in (object[])value)
				{
					Dictionary<string, object> itemDict = (Dictionary<string, object>)item;
					object nameObj = itemDict["name"];
					object valueObj = itemDict["value"];
					metadata.Add((string)nameObj, valueObj.ToString());
				}
			}
		}
	}
}
