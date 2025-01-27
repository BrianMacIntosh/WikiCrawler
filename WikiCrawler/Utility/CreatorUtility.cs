using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Tasks;
using WikiCrawler;

namespace MediaWiki
{
	public static class CreatorUtilityMeta
	{
		public static bool IsInitialized = false;

		/// <summary>
		/// Saves cached creator data out to files.
		/// </summary>
		public static void SaveOut()
		{
			if (IsInitialized)
			{
				CreatorUtility._SaveOut();
			}
		}
	}

	/// <summary>
	/// Removes all creators from the cache that have no useful data.
	/// </summary>
	public class TrimCreators : BaseTask
	{
		public override void Execute()
		{
			CreatorUtility.TrimEmpty();
		}
	}

	/// <summary>
	/// Caches useful information about authors and creators.
	/// </summary>
	public static class CreatorUtility
	{
		private static Dictionary<string, string> s_creatorHomecats = new Dictionary<string, string>();

		private static Dictionary<string, Creator> s_creatorData;
		private static Dictionary<string, string> s_creatorRedirects;

		private static bool s_creatorsDirty = false;
		private static bool s_redirectsDirty = false;

		private static string DataCacheFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "creator_templates.json"); }
		}

		private static string RedirectsCacheFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "creator_redirects.json"); }
		}

		static CreatorUtility()
		{
			// load known creators
			string creatorDataFile = DataCacheFile;
			if (File.Exists(creatorDataFile))
			{
				s_creatorData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Creator>>(
					File.ReadAllText(creatorDataFile, Encoding.UTF8));
				foreach (Creator creator in s_creatorData.Values)
				{
					creator.Usage = 0;
					creator.UploadableUsage = 0;
				}
			}
			else
			{
				s_creatorData = new Dictionary<string, Creator>();
			}

			// load creator redirects
			string creatorRedirectsFile = RedirectsCacheFile;
			if (File.Exists(creatorRedirectsFile))
			{
				s_creatorRedirects = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(
					File.ReadAllText(creatorRedirectsFile, Encoding.UTF8));
			}
			else
			{
				s_creatorRedirects = new Dictionary<string, string>();
			}

			CreatorUtilityMeta.IsInitialized = true;
		}

		public static void TrimEmpty()
		{
			List<KeyValuePair<string, Creator>> kvs = s_creatorData.ToList();
			foreach (var kv in kvs)
			{
				if (kv.Value.IsEmpty)
				{
					s_creatorData.Remove(kv.Key);
					s_creatorsDirty = true;
				}
			}
		}

		public static void ConvertToRedirects()
		{
			List<KeyValuePair<string, Creator>> kvs = s_creatorData.ToList();
			foreach (var kv in kvs)
			{
				if (kv.Value.Author.StartsWith("{{Creator:"))
				{
					// convert to redirect
					Creator creatorCreator = GetCreator(kv.Value.Author);
					creatorCreator.Usage++;
					Creator.Merge(kv.Value, creatorCreator);
					AddRedirect(kv.Key, kv.Value.Author);
					s_creatorData.Remove(kv.Key);
					s_creatorsDirty = true;
				}
			}
		}

		/// <summary>
		/// Saves cached creator data out to files.
		/// </summary>
		public static void _SaveOut()
		{
			if (s_creatorsDirty)
			{
				// write data
				using (StreamWriter writer = new StreamWriter(new FileStream(DataCacheFile, FileMode.Create, FileAccess.Write), Encoding.UTF8))
				{
					writer.WriteLine("{");
					List<KeyValuePair<string, Creator>> creatorsFlat = new List<KeyValuePair<string, Creator>>(s_creatorData);
					bool first = true;
					foreach (KeyValuePair<string, Creator> kv in creatorsFlat
						.OrderByDescending(kv => kv.Value.Usage)
						.OrderByDescending(kv => kv.Value.UploadableUsage))
					{
						if (!first)
						{
							writer.WriteLine(',');
						}
						else
						{
							first = false;
						}
						writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Key));
						writer.Write(":");
						writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value, Newtonsoft.Json.Formatting.Indented));
					}
					writer.WriteLine("\n}");
				}
			}

			if (s_redirectsDirty)
			{
				// write redirects
				using (StreamWriter writer = new StreamWriter(new FileStream(RedirectsCacheFile, FileMode.Create, FileAccess.Write), Encoding.UTF8))
				{
					writer.WriteLine("{");
					bool first = true;
					foreach (var kv in s_creatorRedirects)
					{
						if (!first)
						{
							writer.WriteLine(',');
						}
						else
						{
							first = false;
						}
						writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Key));
						writer.Write(":");
						writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value));
					}
					writer.WriteLine("\n}");
				}
			}

			s_creatorsDirty = false;
			s_redirectsDirty = false;
		}

		public static void AddRedirect(string from, string to)
		{
			from = from.Trim();
			to = to.Trim();

			if (!s_creatorRedirects.ContainsKey(from))
			{
				s_creatorRedirects.Add(from, to);
				s_redirectsDirty = true;
			}
		}

		public static Creator GetCreator(string key)
		{
			bool eat;
			return GetCreator(key, out eat);
		}

		public static Creator GetCreator(string key, out bool isNew)
		{
			key = key.Trim();

			if (s_creatorRedirects.TryGetValue(key, out string redirect))
			{
				key = redirect;
			}

			if (s_creatorData.TryGetValue(key, out Creator creator))
			{
				isNew = false;
				return creator;
			}
			else
			{
				isNew = true;
				creator = CreateNewCreator(key);
				s_creatorData.Add(key, creator);
				s_creatorsDirty = true;
				return creator;
			}
		}

		public static readonly Regex CreatorTemplateRegex = new Regex(@"^{{([Cc]reator:.+)}}$");
		public static readonly Regex AuthorLifespanRegex = new Regex(@"^([^\(\)]+)\s+\(?([0-9][0-9][0-9][0-9])[\-–—]([0-9][0-9][0-9][0-9])\)?$");

		/// <summary>
		/// Creates a new creator, automatically filling in any info possible.
		/// </summary>
		private static Creator CreateNewCreator(string key)
		{
			Creator creator = new Creator();
			
			// see if the name already includes birth and death years
			Match birthDeathMatch = AuthorLifespanRegex.Match(key);
			if (birthDeathMatch.Success)
			{
				if (int.TryParse(birthDeathMatch.Groups[3].Value, out int deathYear))
				{
					creator.DeathYear = deathYear;
				}
			}

			PageTitle creatorTemplate = GetCreatorTemplate(key);
			if (!creatorTemplate.IsEmpty)
			{
				creator.Author = "{{" + creatorTemplate + "}}";
				creator.DeathYear = PdOldAuto.GetCreatorDeathYear(creatorTemplate);
			}

			return creator;
		}

		/// <summary>
		/// If the string represents a creator template, returns that template's page title.
		/// </summary>
		public static PageTitle GetCreatorTemplate(string str)
		{
			Match creatorMatch = CreatorTemplateRegex.Match(str);
			if (creatorMatch.Success)
			{
				return PageTitle.Parse(creatorMatch.Groups[1].Value);
			}
			else
			{
				return PageTitle.Empty;
			}
		}

		public static bool TryGetHomeCategory(string creator, out string homeCategory)
		{
			return s_creatorHomecats.TryGetValue(creator, out homeCategory);
		}

		public static void SetHomeCategory(string creator, string homeCategory)
		{
			s_creatorHomecats[creator] = homeCategory;
		}
	}
}
