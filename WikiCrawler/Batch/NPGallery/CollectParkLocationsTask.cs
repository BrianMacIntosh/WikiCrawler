﻿using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WikiCrawler;

namespace Tasks
{
	public class CollectParkLocations : BaseTask
	{
		private class RawData
		{
			public string item;
			public string itemLabel;
			public string value;
			public string valueLabel;
		}

		public override void Execute()
		{
			string rawPath = Path.Combine(Configuration.DataDirectory, "npsunits.json");
			RawData[] rawData = JsonConvert.DeserializeObject<RawData[]>(File.ReadAllText(rawPath, Encoding.UTF8));

			Api wikidata = new Api(new System.Uri("https://www.wikidata.org/"));
			wikidata.AutoLogIn();

			List<string> entityIds = new List<string>();
			foreach (RawData raw in rawData)
			{
				string qCode = raw.item.Substring(raw.item.LastIndexOf('/') + 1);
				entityIds.Add(qCode);
			}

			// maps q-codes to Commons Categories
			Dictionary<string, string> commonsCats = new Dictionary<string, string>();

			// maps locations to their immediate parents along P131
			Dictionary<string, List<string>> locationParents = new Dictionary<string, List<string>>();

			Entity[] entities = wikidata.GetEntities(entityIds, props: Api.BuildParameterList(WBProp.info, WBProp.claims));

			while (entities != null && entities.Length > 0)
			{
				entityIds.Clear();
				
				foreach (Entity entity in entities)
				{
					Claim[] locationClaims;
					if (entity.claims.TryGetValue("P131", out locationClaims))
					{
						foreach (Claim claim in locationClaims)
						{
							Dictionary<string, object> value = (Dictionary<string, object>)claim.mainSnak.datavalue["value"];
							string id = (string)value["id"];
							entityIds.AddUnique(id);

							// remember as a parent
							List<string> parents;
							if (locationParents.TryGetValue(entity.id, out parents))
							{
								parents.AddUnique(id);
							}
							else
							{
								locationParents.Add(entity.id, new List<string>() { id });
							}
						}
					}

					Claim[] commonsCat;
					if (entity.claims.TryGetValue("P373", out commonsCat))
					{
						string catName = (string)commonsCat[0].mainSnak.datavalue["value"];
						commonsCats[entity.id] = catName;
					}
				}
				
				entities = wikidata.GetEntities(entityIds, props: Api.BuildParameterList(WBProp.info, WBProp.claims));
			}

			// for each NPS unit, collect all of its parent commons cats
			Dictionary<string, List<string>> unitParents = new Dictionary<string, List<string>>();
			foreach (RawData raw in rawData)
			{
				// collect all parent ids
				string qCode = raw.item.Substring(raw.item.LastIndexOf('/') + 1);
				List<string> parentIds = new List<string>() { qCode };
				for (int i = 0; i < parentIds.Count; i++)
				{
					if (locationParents.TryGetValue(parentIds[i], out List<string> thisParents))
					{
						parentIds.AddRangeUnique(thisParents);
					}
				}

				// map IDs to cats
				List<string> parentCats = new List<string>(parentIds.Count);
				foreach (string parentId in parentIds)
				{
					if (commonsCats.TryGetValue(parentId, out string commonsCat))
					{
						parentCats.Add(commonsCat);
					}
				}

				// store cat list
				if (unitParents.ContainsKey(raw.value))
				{
					Console.WriteLine(raw.value);
				}
				unitParents[raw.value] = parentCats;
			}

			string resultPath = Path.Combine(Configuration.DataDirectory, "npsunit-parents.json");
			File.WriteAllText(resultPath, JsonConvert.SerializeObject(unitParents), Encoding.UTF8);
		}
	}
}
