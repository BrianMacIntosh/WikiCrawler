using System.Collections.Generic;

namespace MediaWiki
{
	public class Snak
	{
		public string snaktype;
		public string property;
		public string datatype;
		public Dictionary<string, object> datavalue;

		public Snak(Dictionary<string, object> json)
		{
			snaktype = (string)json["snaktype"];
			property = (string)json["property"];
			if (json.ContainsKey("datatype"))
				datatype = (string)json["datatype"];
			if (json.ContainsKey("datavalue"))
				datavalue = (Dictionary<string, object>)json["datavalue"];
		}

		public Snak(string value)
		{
			//HACK:
			datavalue = new Dictionary<string, object>
			{
				{ "value", value }
			};
		}

		/*public string GetSerialized()
		{
			//TODO:
		}*/

		public Entity GetValueAsEntity(Api api)
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			string pagename = "Q" + (int)entityValue["numeric-id"];
			return api.GetEntity(pagename);
		}

		public int GetValueAsEntityId()
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			return (int)entityValue["numeric-id"];
		}

		public string GetValueAsString()
		{
			return (string)datavalue["value"];
		}

		public string GetValueAsGender()
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			switch ((int)entityValue["numeric-id"])
			{
				case 6581072:
				case 1052281:
					return "female";
				case 6581097:
				case 2449503:
					return "male";
				default:
					return "";
			}
		}

		public DateTime GetValueAsDate()
		{
			if (datavalue == null)
			{
				return null;
			}

			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];

			int precision = (int)entityValue["precision"];
			return new DateTime((string)entityValue["time"], precision);
		}
	}
}
