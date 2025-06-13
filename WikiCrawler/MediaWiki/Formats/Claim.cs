using System;
using System.Collections.Generic;

namespace MediaWiki
{
	public class Claim
	{
		public string id;
		public Snak mainSnak;
		public Dictionary<string, Snak[]> qualifiers;
		public string type;
		public WikidataRank rank;

		public Claim(Dictionary<string, object> json)
		{
			id = (string)json["id"];
			mainSnak = new Snak((Dictionary<string, object>)json["mainsnak"]);

			if (json.ContainsKey("qualifiers"))
			{
				qualifiers = new Dictionary<string, Snak[]>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, object> qualifiersData = (Dictionary<string, object>)json["qualifiers"];
				foreach (KeyValuePair<string, object> kv in qualifiersData)
				{
					object[] snakJsonArray = (object[])kv.Value;
					Snak[] snakArray = new Snak[snakJsonArray.Length];
					for (int c = 0; c < snakArray.Length; c++)
					{
						snakArray[c] = new Snak((Dictionary<string, object>)snakJsonArray[c]);
					}
					qualifiers[kv.Key] = snakArray;
				}
			}

			type = (string)json["type"];
			rank = Wikidata.ParseRankChecked((string)json["rank"]);
		}

		public Claim(string value)
		{
			//HACK:
			mainSnak = new Snak(value);
		}

		public string GetSerialized()
		{
			throw new NotImplementedException();
			return "{\"id\":\"Q2$5627445f-43cb-ed6d-3adb-760e85bd17ee\",\"type\":\"" + type + "\",\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P1\",\"datavalue\":{\"value\":\"City\",\"type\":\"string\"}}}";
		}

		public bool HasValue()
		{
			return mainSnak != null && mainSnak.snaktype == SnakType.Value;
		}

		public Snak[] GetQualifiers(string propId)
		{
			if (qualifiers != null && qualifiers.TryGetValue(propId, out Snak[] qualifierSnaks))
			{
				return qualifierSnaks;
			}
			else
			{
				return null;
			}
		}
	}
}
