using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaWiki
{
	public class Entity : Object
	{
		public string modified;
		public QId id;
		public string type;

		//indexed by language
		public Dictionary<string, string[]> aliases;
		public Dictionary<string, string> labels;
		public Dictionary<string, string> descriptions;

		public Dictionary<string, Claim[]> claims;

		public Dictionary<string, string> sitelinks;

		public static bool IsNullOrMissing(Entity entity)
		{
			return entity == null || entity.missing;
		}

		public Entity()
		{

		}

		public Entity(Dictionary<string, object> json)
			: base(json)
		{
			if (json.ContainsKey("modified"))
				modified = (string)json["modified"];
			if (json.ContainsKey("id"))
				id = QId.Parse((string)json["id"]);
			if (json.ContainsKey("type"))
				type = (string)json["type"];

			if (json.ContainsKey("aliases"))
				aliases = ParseLanguageValueArray((Dictionary<string, object>)json["aliases"]);
			if (json.ContainsKey("labels"))
				labels = ParseLanguageValue((Dictionary<string, object>)json["labels"]);
			if (json.ContainsKey("descriptions"))
				descriptions = ParseLanguageValue((Dictionary<string, object>)json["descriptions"]);

			claims = new Dictionary<string, Claim[]>(StringComparer.OrdinalIgnoreCase);
			if (json.ContainsKey("claims"))
			{
				Dictionary<string, object> claimData = (Dictionary<string, object>)json["claims"];
				foreach (KeyValuePair<string, object> kv in claimData)
				{
					object[] claimJsonArray = (object[])kv.Value;
					Claim[] claimArray = new Claim[claimJsonArray.Length];
					for (int c = 0; c < claimArray.Length; c++)
					{
						claimArray[c] = new Claim((Dictionary<string, object>)claimJsonArray[c]);
					}
					claims[kv.Key] = claimArray;
				}
			}

			sitelinks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (json.ContainsKey("sitelinks"))
			{
				Dictionary<string, object> sitelinkData = (Dictionary<string, object>)json["sitelinks"];
				foreach (KeyValuePair<string, object> kv in sitelinkData)
				{
					Dictionary<string, object> valueDict = (Dictionary<string, object>)kv.Value;
					if (valueDict.ContainsKey("title"))
					{
						sitelinks[kv.Key] = (string)valueDict["title"];
					}
				}
			}
		}

		private static Dictionary<string, string[]> ParseLanguageValueArray(
			Dictionary<string, object> json)
		{
			Dictionary<string, string[]> result = new Dictionary<string, string[]>();
			foreach (KeyValuePair<string, object> kv in json)
			{
				object[] aliasObjList = (object[])kv.Value;
				string[] aliasList = new string[aliasObjList.Length];
				for (int c = 0; c < aliasObjList.Length; c++)
				{
					Dictionary<string, object> arrayItem = (Dictionary<string, object>)aliasObjList[c];
					aliasList[c] = (string)arrayItem["value"];
				}
				result[kv.Key] = aliasList;
			}
			return result;
		}

		private static Dictionary<string, string> ParseLanguageValue(
			Dictionary<string, object> json)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			foreach (KeyValuePair<string, object> kv in json)
			{
				result[kv.Key] = (string)((Dictionary<string, object>)kv.Value)["value"];
			}
			return result;
		}

		public bool HasExactName(string name, bool caseSensitive)
		{
			if (aliases != null)
			{
				foreach (string[] valueList in aliases.Values)
				{
					foreach (string value in valueList)
					{
						if (string.Compare(value, name, !caseSensitive) == 0)
							return true;
					}
				}
			}

			if (labels != null)
			{
				foreach (string value in labels.Values)
				{
					if (string.Compare(value, name, !caseSensitive) == 0)
						return true;
				}
			}

			return false;
		}

		public bool HasClaim(string id)
		{
			return claims != null && claims.ContainsKey(id)
				&& claims[id].Length > 0 && claims[id][0].mainSnak.datavalue != null;
		}

		public Entity GetClaimValueAsEntity(string property, Api api)
		{
			return claims[property][0].mainSnak.GetValueAsEntity(api);
		}

		public IEnumerable<Entity> GetClaimValuesAsEntities(string property, Api api)
		{
			if (claims.TryGetValue(property, out var subclaims))
			{
				return api.GetEntities(subclaims
					.Where(c => c.HasValue())
					.Select(c => c.mainSnak.GetValueAsEntityId())
					.ToArray());
			}
			else
			{
				return new Entity[0];
			}
		}

		public QId GetClaimValueAsEntityId(string property)
		{
			return claims[property][0].mainSnak.GetValueAsEntityId();
		}

		public IEnumerable<QId> GetClaimValuesAsEntityIds(string property)
		{
			if (claims.TryGetValue(property, out Claim[] subclaims))
			{
				foreach (Claim subclaim in subclaims)
				{
					if (subclaim.HasValue()) //TODO: return null?
					{
						yield return subclaim.mainSnak.GetValueAsEntityId();
					}
				}
			}
			else
			{
				yield break;
			}
		}

		public string GetClaimValueAsString(string property)
		{
			return claims[property][0].mainSnak.GetValueAsString();
		}

		public bool TryGetClaimValueAsString(string property, out string[] value)
		{
			if (claims.TryGetValue(property, out Claim[] cvalue))
			{
				value = new string[cvalue.Length];
				for (int i = 0; i < cvalue.Length; i++)
				{
					value[i] = cvalue[i].mainSnak.GetValueAsString();
				}
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		}

		public string[] GetClaimValuesAsString(string property)
		{
			Claim[] subclaims = claims[property];
			string[] result = new string[subclaims.Length];
			for (int c = 0; c < subclaims.Length; c++)
				result[c] = subclaims[c].mainSnak.GetValueAsString();
			return result;
		}

		public string GetClaimValueAsGender(string property)
		{
			return claims[property][0].mainSnak.GetValueAsGender();
		}

		private readonly static DateTime[] s_emptyDateTime = new DateTime[0];

		public IEnumerable<DateTime> GetClaimValuesAsDates(string property)
		{
			if (claims.TryGetValue(property, out Claim[] propClaims))
			{
				return propClaims.Where(c => c.HasValue()).Select(c => c.mainSnak.GetValueAsDate());
			}
			else
			{
				return s_emptyDateTime;
			}
		}

		public bool TryGetClaimValuesAsDates(string property, out IEnumerable<DateTime> dates)
		{
			if (claims.TryGetValue(property, out Claim[] propClaims))
			{
				dates = propClaims.Where(c => c.HasValue()).Select(c => c.mainSnak.GetValueAsDate());
				return true;
			}
			else
			{
				dates = s_emptyDateTime;
				return false;
			}
		}
	}
}
