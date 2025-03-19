using MediaWiki;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WikiCrawler;

namespace Tasks
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

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			PdArtReplacement.SkipCached = false;
			ImplicitCreatorsReplacement.SlowCategoryWalk = false; // already tried last time
			PdArtReplacement.SkipAuthorLookup = true; // already tried last time

			string pdArtDirectory = Path.Combine(Configuration.DataDirectory, "PdArtReplacement");
			ManualMapping<MappingCreator> creatorMapping = new ManualMapping<MappingCreator>(ImplicitCreatorsReplacement.GetCreatorMappingFile(pdArtDirectory));
			ManualMapping<MappingDate> dateMapping = new ManualMapping<MappingDate>(PdArtReplacement.DateMappingFile);

			foreach (var kv in creatorMapping)
			{
				if (!string.IsNullOrEmpty(kv.Value.MappedValue)
					|| !string.IsNullOrEmpty(kv.Value.MappedQID))
				{
					//TODO: GetPages should automatically break up file lists
					foreach (Article article in GlobalAPIs.Commons.FetchArticles(kv.Value.FromPages.Select(title => new Article(title))))
					{
						yield return article;
					}
				}
			}

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
		}
	}
}
