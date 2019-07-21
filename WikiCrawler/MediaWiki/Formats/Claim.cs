using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaWiki
{
	public class Claim
	{
		public string id;
		public Snak mainSnak;
		public string type;
		public string rank;

		public Claim(Dictionary<string, object> json)
		{
			id = (string)json["id"];
			mainSnak = new Snak((Dictionary<string, object>)json["mainsnak"]);
			type = (string)json["type"];
			rank = (string)json["rank"];
		}

		public string GetSerialized()
		{
			return "{\"id\":\"Q2$5627445f-43cb-ed6d-3adb-760e85bd17ee\",\"type\":\"" + type + "\",\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P1\",\"datavalue\":{\"value\":\"City\",\"type\":\"string\"}}}";
		}
	}
}
