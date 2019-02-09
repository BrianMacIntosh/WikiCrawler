using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	public static class CreatorUtility
	{
		private static Dictionary<string, string> s_creatorHomecats = new Dictionary<string, string>();

		private static Dictionary<string, Creator> s_creatorData = new Dictionary<string, Creator>();

		private static string CacheFile
		{
			get { return Path.Combine(Configuration.DataDirectory, "creator_templates.json"); }
		}

		static CreatorUtility()
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
				}
			}

			ValidateCreators();
		}

		/// <summary>
		/// Saves cached creator data out to files.
		/// </summary>
		public static void SaveOut()
		{
			//write creators
			string creatorTemplatesFile = CacheFile;
			File.WriteAllText(
				creatorTemplatesFile,
				Newtonsoft.Json.JsonConvert.SerializeObject(s_creatorData, Newtonsoft.Json.Formatting.Indented),
				Encoding.UTF8);
		}

		private static void ValidateCreators()
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
			if (s_creatorData.TryGetValue(key, out creator))
			{
				return true;
			}
			else
			{
				s_creatorData.Add(key, new Creator());
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
