using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WikiCrawler;

namespace Tasks
{
	public class CollectParkLocations : BaseTask
	{
		private class RawData
		{
#pragma warning disable 0649
			public string item;
			public string itemLabel;
			public string value;
			public string valueLabel;
#pragma warning restore 0649
		}

		public override void Execute()
		{
			string rawPath = Path.Combine(Configuration.DataDirectory, "npsunits.json");
			RawData[] rawData = JsonConvert.DeserializeObject<RawData[]>(File.ReadAllText(rawPath, Encoding.UTF8));

			List<QId> entityIds = new List<QId>();
			foreach (RawData raw in rawData)
			{
				string qidString = raw.item.Substring(raw.item.LastIndexOf('/') + 1);
				entityIds.Add(QId.Parse(qidString));
			}

			// maps q-codes to Commons Categories
			Dictionary<QId, PageTitle> commonsCats = new Dictionary<QId, PageTitle>();

			// maps locations to their immediate parents along P131
			Dictionary<QId, List<QId>> locationParents = new Dictionary<QId, List<QId>>();

			IEnumerable<Entity> entities = GlobalAPIs.Wikidata.GetEntities(entityIds, props: Api.BuildParameterList(WBProp.info, WBProp.claims));

			while (entities != null && entities.Any())
			{
				entityIds.Clear();
				
				foreach (Entity entity in entities)
				{
					Claim[] locationClaims;
					if (entity.claims.TryGetValue(Wikidata.Prop_LocatedInTerritory, out locationClaims))
					{
						foreach (Claim claim in locationClaims)
						{
							//TODO: use rank
							QId id = claim.mainSnak.GetValueAsEntityId();
							entityIds.AddUnique(id);

							// remember as a parent
							List<QId> parents;
							if (locationParents.TryGetValue(entity.id, out parents))
							{
								parents.AddUnique(id);
							}
							else
							{
								locationParents.Add(entity.id, new List<QId>() { id });
							}
						}
					}

					Claim[] commonsCat;
					if (entity.claims.TryGetValue(Wikidata.Prop_CommonsCategory, out commonsCat))
					{
						string catName = commonsCat[0].mainSnak.GetValueAsString();
						commonsCats[entity.id] = new PageTitle(PageTitle.NS_File, catName);
					}
				}
				
				entities = GlobalAPIs.Wikidata.GetEntities(entityIds, props: Api.BuildParameterList(WBProp.info, WBProp.claims));
			}

			// for each NPS unit, collect all of its parent commons cats
			Dictionary<string, List<string>> unitParents = new Dictionary<string, List<string>>();
			foreach (RawData raw in rawData)
			{
				// collect all parent ids
				QId qCode = QId.Parse(raw.item.Substring(raw.item.LastIndexOf('/') + 1));
				List<QId> parentIds = new List<QId>() { qCode };
				for (int i = 0; i < parentIds.Count; i++)
				{
					if (locationParents.TryGetValue(parentIds[i], out List<QId> thisParents))
					{
						parentIds.AddRangeUnique(thisParents);
					}
				}

				// map IDs to cats
				List<PageTitle> parentCats = new List<PageTitle>(parentIds.Count);
				foreach (QId parentId in parentIds)
				{
					if (commonsCats.TryGetValue(parentId, out PageTitle commonsCat))
					{
						parentCats.Add(commonsCat);
					}
				}

				// store cat list
				if (unitParents.ContainsKey(raw.value))
				{
					Console.WriteLine(raw.value);
				}
				unitParents[raw.value] = parentCats.Select(t => t.Name).ToList();
			}

			string resultPath = Path.Combine(Configuration.DataDirectory, "npsunit-parents.json");
			File.WriteAllText(resultPath, JsonConvert.SerializeObject(unitParents), Encoding.UTF8);
		}
	}
}
