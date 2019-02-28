using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public abstract class ContentDmDownloader : BatchDownloader
{
	public ContentDmDownloader(string key)
		: base(key)
	{

	}

	protected override Dictionary<string, string> ParseMetadata(string pageContent)
	{
		// pull out the metadata section
		int metaStartIndex = pageContent.IndexOf("<script>");
		if (metaStartIndex < 0) throw new UWashException("No metadata found in page");
		metaStartIndex += "<script>".Length;
		string dataText = pageContent.Substring(metaStartIndex);
		dataText = dataText.Substring(0, dataText.IndexOf("</script>") - 1);
		dataText = dataText.Trim();

		// grab the JSON content
		int leaderLength = "window.__INITIAL_STATE__ = JSON.parse('".Length;
		dataText = dataText.Substring(leaderLength, dataText.Length - (leaderLength + "');".Length));

		// unescape JSON
		dataText = Regex.Unescape(dataText);

		// parse JSON
		Dictionary<string, object> deser = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataText);

		if (!deser.ContainsKey("item"))
		{
			// that's weird. Try again.
			throw new RedownloadException();
		}

		Newtonsoft.Json.Linq.JObject item = (Newtonsoft.Json.Linq.JObject)deser["item"];
		if ((string)item["state"] == "notFound")
		{
			return null;
		}
		item = (Newtonsoft.Json.Linq.JObject)item["item"];
		object[] fields = item["fields"].ToObject<object[]>();

		Dictionary<string, string> data = new Dictionary<string, string>();

		foreach (object field in fields)
		{
			Newtonsoft.Json.Linq.JObject fieldData = (Newtonsoft.Json.Linq.JObject)field;

			//TODO: CleanHtml?
			string value = fieldData["value"].ToObject<string>();
			value = value.TrimStart('[').TrimEnd(']');

			data[(string)fieldData["label"]] = value;
		}

		return data;
	}
}
