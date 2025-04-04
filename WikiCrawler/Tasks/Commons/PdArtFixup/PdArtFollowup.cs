using MediaWiki;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikiCrawler;

namespace Tasks.Commons
{
	/// <summary>
	/// Reruns the PdArtReplacement on files from creator and date mappings that have been mapped.
	/// </summary>
	public class PdArtFollowup : ReplaceIn
	{
		public PdArtFollowup()
			: base(PdArtFixup.CreateReplacement())
		{
			HeartbeatEnabled = true;
		}

		public override void Execute()
		{
			base.Execute();

			// any dates where the raw date parses now can be dropped
			ManualMapping<MappingDate> dateMapping = new ManualMapping<MappingDate>(PdArtReplacement.DateMappingFile);
			foreach (string key in dateMapping.Keys.ToArray())
			{
				if (PdArtReplacement.ParseDate(key).LatestYear != 9999)
				{
					dateMapping.Remove(key);
					dateMapping.SetDirty();
				}
			}
			dateMapping.Serialize();
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time
			PdArtReplacement.SkipAuthorLookup = true; // already tried last time

			string pdArtDirectory = Path.Combine(Configuration.DataDirectory, "PdArtReplacement");
			ManualMapping<MappingCreator> creatorMapping = new ManualMapping<MappingCreator>(ImplicitCreatorsReplacement.GetCreatorMappingFile(pdArtDirectory));
			ManualMapping<MappingDate> dateMapping = new ManualMapping<MappingDate>(PdArtReplacement.DateMappingFile);

			foreach (var kv in dateMapping)
			{
				if (!string.IsNullOrEmpty(kv.Value.ReplaceDate)
					&& PdArtReplacement.ParseDate(kv.Value.ReplaceDate).LatestYear < System.DateTime.Now.Year - 95)
				{
					foreach (Article article in GlobalAPIs.Commons.FetchArticles(kv.Value.FromPages.Select(title => new Article(title))))
					{
						yield return article;
					}
				}
				else if (kv.Value.LatestYear < System.DateTime.Now.Year - 95
					|| PdArtReplacement.ParseDate(kv.Key).LatestYear < System.DateTime.Now.Year - 95)
				{
					foreach (Article article in GlobalAPIs.Commons.FetchArticles(kv.Value.FromPages.Select(title => new Article(title))))
					{
						yield return article;
					}
				}
			}

			yield break;

			foreach (var kv in creatorMapping)
			{
				if (kv.Value.MappedDeathyear != 9999
					|| !string.IsNullOrEmpty(kv.Value.MappedValue)
					|| !string.IsNullOrEmpty(kv.Value.MappedQID))
				{
					//TODO: GetPages should automatically break up file lists
					foreach (Article article in GlobalAPIs.Commons.FetchArticles(kv.Value.FromPages.Select(title => new Article(title))))
					{
						yield return article;
					}
				}
			}
		}
	}
}
