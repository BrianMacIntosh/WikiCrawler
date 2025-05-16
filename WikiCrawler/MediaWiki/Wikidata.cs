using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace MediaWiki
{
	public enum WikidataRank
	{
		Deprecated,
		Normal,
		Preferred,
	}

	public static class Wikidata
	{
		public const string Prop_CountryOfCitizenship = "P27";
		public const string Prop_InstanceOf = "P31";
		public const string Prop_Creator = "P170";
		public const string Prop_CommonsCategory = "P373";
		public const string Prop_DateOfBirth = "P569";
		public const string Prop_DateOfDeath = "P570";
		public const string Prop_Inception = "P571";
		public const string Prop_PublicationDate = "P577";
		public const string Prop_CommonsCreator = "P1472";

		public const int Entity_Human = 5;

		private static readonly Regex QidRegex = new Regex("Q([0-9]+)", RegexOptions.IgnoreCase);

		/// <summary>
		/// Converts a qid like 'Q1000' into an int 1000.
		/// </summary>
		public static int? UnQidify(string qid)
		{
			if (TryUnQidify(qid, out int outQid))
			{
				return outQid;
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Converts a qid like 'Q1000' into an int 1000.
		/// </summary>
		public static int UnQidifyChecked(string qid)
		{
			if (TryUnQidify(qid, out int outQid))
			{
				return outQid;
			}
			else
			{
				Debug.Assert(false, string.Format("Failed to UnQidify '{0}'."), qid);
				return 0;
			}
		}

		/// <summary>
		/// Converts a qid like 'Q1000' into an int 1000.
		/// </summary>
		public static bool TryUnQidify(string qid, out int outQid)
		{
			Match match = QidRegex.Match(qid);
			if (match.Success)
			{
				outQid = int.Parse(match.Groups[1].Value);
				return true;
			}
			else
			{
				outQid = 0;
				return false;
			}
		}

		/// <summary>
		/// Gets the Wikidata property id for the specified parameter of Commons "Authority control".
		/// </summary>
		/// <param name="authority"></param>
		/// <returns></returns>
		public static string GetPropertyIdForAuthority(string authority)
		{
			switch (authority)
			{
				case "VIAF":
					return "P214";
				case "LCCN":
					return "P244";
				case "ARC":
					return "";
				case "GND":
					return "P227";
				case "SELIBR":
					return "";
				case "BNF":
					return "P268";
				case "SUDOC":
					return "P269";
				case "BPN":
					return "P651";
				case "ULAN":
					return "P245";
				case "ORCID":
					return "P496";
				case "RID":
					return "";
				case "ISNI":
					return "P213";
				case "BIBSYS":
					return "P1015";
				case "KID":
					return "P1138";
				case "FotoBibl":
					return "";
				case "Museofile":
					return "P539";
				case "TSURL":
					return "";
				default:
					return "";
			}
		}

		public static string[] CommonsAuthorities = new string[]
		{
			"VIAF",
			"LCCN",
			"GND",
			"BNF",
			"SUDOC",
			"ULAN",
			"ORCID",
			"RID",
			"ISNI",
			"SELIBR",
			"BPN",
			"BIBSYS",
			"KID",
			"Museofile",
		};

		/// <summary>
		/// Returns true if the specified entity has the specified Commons authority claim.
		/// </summary>
		public static bool EntityHasAuthority(string authority, Entity entity)
		{
			return entity.HasClaim(GetPropertyIdForAuthority(authority));
		}

		public static string ConvertAuthorityFromCommonsToWikidata(string authority, string value)
		{
			try
			{
				if (authority == "LCCN")
				{
					string[] split = value.Split('/');
					if (split.Length == 3)
					{
						int serial = int.Parse(split[2]);
						return split[0] + split[1] + serial.ToString("000000");
					}
					else
					{
						return value;
					}
				}
				else if (authority == "ISNI")
				{
					return value.Substring(0, 4) + " " +
						value.Substring(4, 4) + " " +
						value.Substring(8, 4) + " " +
						value.Substring(12, 4);
				}
				else if (authority == "BNF")
				{
					if (value.StartsWith("cb"))
					{
						return value.Substring(2);
					}
					else
					{
						return value;
					}
				}
				else
				{
					return value;
				}
			}
			catch (FormatException)
			{
				return value;
			}
		}

		public static string ConvertAuthorityFromWikidataToCommons(string authority, Entity entity)
		{
			string claim = GetPropertyIdForAuthority(authority);
			if (!string.IsNullOrEmpty(claim))
			{
				return ConvertAuthorityFromWikidataToCommons(authority, entity.GetClaimValueAsString(claim));
			}
			else
			{
				return "";
			}
		}

		public static string ConvertAuthorityFromWikidataToCommons(string authority, string value)
		{
			if (authority == "LCCN")
			{
				string alpha = "";
				string year = "";
				string serial = "";
				for (int c = 0; c < value.Length; c++)
				{
					if (char.IsDigit(value[c]))
					{
						alpha = value.Substring(0, c);
						break;
					}
				}
				year = value.Substring(alpha.Length, value.Length - alpha.Length - 6);
				serial = value.Substring(value.Length - 6, 6);

				return alpha + "/" + year + "/" + serial.TrimStart('0');
			}
			else if (authority == "ISNI")
			{
				return value.Replace(" ", "");
			}
			else if (authority == "BNF")
			{
				return "cb" + value;
			}
			else
			{
				return value;
			}
		}

		/// <summary>
		/// Creates an authority control template for the specified entity.
		/// </summary>
		/// <param name="additionalOptions"></param>
		/// <returns></returns>
		public static string GetAuthorityControlTemplate(Entity entity,
			string additionalOptions,
			Dictionary<string, string> additionalKeys)
		{
			string result = "";

			foreach (string auth in CommonsAuthorities)
			{
				if (EntityHasAuthority(auth, entity))
				{
					result += "|" + auth + "=" + ConvertAuthorityFromWikidataToCommons(auth, entity);
				}
			}
			if (additionalKeys != null)
			{
				foreach (KeyValuePair<string, string> kv in additionalKeys)
				{
					if (!EntityHasAuthority(kv.Key, entity))
					{
						result += "|" + kv.Key + "=" + kv.Value;
					}
				}
			}

			if (!string.IsNullOrEmpty(result))
			{
				// wikidata by itself is not enough to warrant the template
				result += "|Q=" + entity.id;

				if (!string.IsNullOrEmpty(additionalOptions))
				{
					result += "|" + additionalOptions;
				}

				return "{{Authority control" + result + "}}";
			}
			else
			{
				return "";
			}
		}

		public static WikidataRank ParseRankChecked(string rank)
		{
			if (rank.Equals("deprecated", StringComparison.InvariantCultureIgnoreCase))
			{
				return WikidataRank.Deprecated;
			}
			else if (rank.Equals("normal", StringComparison.InvariantCultureIgnoreCase))
			{
				return WikidataRank.Normal;
			}
			else if (rank.Equals("preferred", StringComparison.InvariantCultureIgnoreCase))
			{
				return WikidataRank.Preferred;
			}
			else
			{
				throw new ArgumentException(string.Format("Unrecognized wikidata rank '{0}'.", rank));
			}
		}

		private static readonly Claim[] s_emptyClaims = new Claim[0];

		/// <summary>
		/// Keeps only the items in the enumerable with the maximum rank.
		/// </summary>
		public static IEnumerable<Claim> KeepBestRank(IEnumerable<Claim> claims, WikidataRank minimumRank = WikidataRank.Normal)
		{
			WikidataRank maxRank = claims.Select(claim => claim.rank).Max();
			if (maxRank < minimumRank)
			{
				return s_emptyClaims;
			}
			else
			{
				return claims.Where(claim => claim.rank == maxRank);
			}
		}
	}
}
