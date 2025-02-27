using MediaWiki;
using System.Collections.Generic;
using System.IO;
using WikiCrawler;

namespace Tasks
{
	public class PdArtFollowup : ReplaceIn
	{
		public PdArtFollowup()
			: base(PdArtFixup.CreateReplacement())
		{
			HeartbeatEnabled = true;
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false;

			ManualMapping<MappingDate> dateMapping = new ManualMapping<MappingDate>(PdArtReplacement.DateMappingFile);
			foreach (var kv in dateMapping)
			{
				if (!string.IsNullOrEmpty(kv.Value.ReplaceDate)
					|| kv.Value.LatestYear != 9999
					|| PdArtReplacement.ParseDate(kv.Key).LatestYear != 9999)
				{
					Article[] articles = GlobalAPIs.Commons.GetPages(kv.Value.FromPages);
					foreach (Article article in articles)
					{
						yield return article;
					}
				}
			}

			string pdArtDirectory = Path.Combine(Configuration.DataDirectory, "PdArtFixup");
			ManualMapping<MappingCreator> creatorMapping = new ManualMapping<MappingCreator>(ImplicitCreatorsReplacement.GetCreatorMappingFile(pdArtDirectory));
			foreach (var kv in creatorMapping)
			{
				if (!string.IsNullOrEmpty(kv.Value.MappedValue)
					|| !string.IsNullOrEmpty(kv.Value.MappedQID))
				{
					Article[] articles = GlobalAPIs.Commons.GetPages(kv.Value.FromPages);
					foreach (Article article in articles)
					{
						yield return article;
					}
				}
			}
		}
	}
}
