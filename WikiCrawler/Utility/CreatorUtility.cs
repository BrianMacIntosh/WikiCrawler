using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WikiCrawler
{
	public static class CreatorUtility
	{
		private static Dictionary<string, string> s_creatorHomecats = new Dictionary<string, string>();

		private static Dictionary<string, Creator> s_creatorData;

		private static string CacheFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "creator_templates.json"); }
		}

		public static void Initialize(Wikimedia.WikiApi api)
		{
			//load known creators
			string creatorTemplatesFile = CacheFile;
			if (File.Exists(creatorTemplatesFile))
			{
				s_creatorData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Creator>>(
					File.ReadAllText(creatorTemplatesFile, Encoding.UTF8));
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

			ValidateCreators(api);
		}

		/// <summary>
		/// Saves cached creator data out to files.
		/// </summary>
		public static void SaveOut()
		{
			//write creators
			string creatorTemplatesFile = CacheFile;

			using (StreamWriter writer = new StreamWriter(new FileStream(creatorTemplatesFile, FileMode.Create, FileAccess.Write), Encoding.UTF8))
			{
				writer.WriteLine("{");
				List<KeyValuePair<string, Creator>> creatorsFlat = new List<KeyValuePair<string, Creator>>(s_creatorData);
				bool first = true;
				foreach (KeyValuePair<string, Creator> kv in creatorsFlat.OrderByDescending(kv => kv.Value.UploadableUsage))
				{
					if (!first)
					{
						writer.WriteLine(',');
					}
					else
					{
						first = false;
					}
					writer.Write("\"" + kv.Key + "\":");
					writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value, Newtonsoft.Json.Formatting.Indented));
				}
				writer.WriteLine("}");
			}
		}

		private static void ValidateCreators(Wikimedia.WikiApi api)
		{
			Console.WriteLine("Validating creators...");
			foreach (KeyValuePair<string, Creator> kv in s_creatorData)
			{
				Creator data = kv.Value;
				Console.WriteLine(kv.Key);

				string attempt = kv.Key;

				if (attempt.StartsWith("Creator:"))
					attempt = attempt.Substring("Creator:".Length);

				if (string.IsNullOrEmpty(data.Author) && !string.IsNullOrEmpty(attempt))
				{
					//1. validate suggested creator
					//Wikimedia.Article creatorArt = Api.GetPage("Creator:" + attempt);
					//if (creatorArt != null && !creatorArt.missing)
					//{
					//	data.Author = "{{" + creatorArt.title + "}}";
					//}

					//2. try to create suggested creator
					if (string.IsNullOrEmpty(data.Author))
					{
						//string trying = attempt;
						//Console.WriteLine("Attempting creation...");
						//if (CommonsCreatorFromWikidata.TryMakeCreator(Api, ref trying))
						//	data.Succeeeded = trying;
					}
				}
			}
		}

		public static bool TryGetCreator(string key, out Creator creator)
		{
			if (s_creatorData == null)
			{
				throw new InvalidOperationException("CreatorUtility not initialized");
			}

			if (s_creatorData.TryGetValue(key, out creator))
			{
				return true;
			}
			else
			{
				creator = new Creator();
				s_creatorData.Add(key, creator);
				return false;
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
