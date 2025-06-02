using MediaWiki;
using System.Collections.Generic;
using System.Linq;

namespace Tasks.Commons
{
	/// <summary>
	/// Reruns the PdArtReplacement on files from creator and date mappings that have been mapped.
	/// </summary>
	public class PdArtFollowup : ReplaceIn
	{
		public PdArtFollowup()
			: base(new ImplicitCreatorsReplacement("PdArtReplacement"),
				  new LocalizeDateReplacement(),
				  new PdArtReplacement())
		{

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

			ManualMapping<MappingCreator> creatorMapping = new ManualMapping<MappingCreator>(ImplicitCreatorsReplacement.CreatorMappingFile);
			ManualMapping<MappingDate> dateMapping = new ManualMapping<MappingDate>(PdArtReplacement.DateMappingFile);

#if false
			foreach (var kv in dateMapping)
			{
				if (!string.IsNullOrEmpty(kv.Value.ReplaceDate)
					&& PdArtReplacement.ParseDate(kv.Value.ReplaceDate).LatestYear < System.DateTime.Now.Year - 95)
				{
					foreach (Article article in kv.Value.FromPages.Select(title => new Article(PageTitle.Parse(title))))
					{
						yield return article;
					}
				}
				else if (kv.Value.LatestYear < System.DateTime.Now.Year - 95
					|| PdArtReplacement.ParseDate(kv.Key).LatestYear < System.DateTime.Now.Year - 95)
				{
					foreach (Article article in kv.Value.FromPages.Select(title => new Article(PageTitle.Parse(title))))
					{
						yield return article;
					}
				}
				else if (PdArtReplacement.ParseDate(kv.Key).LatestYear < System.DateTime.Now.Year - 95)
				{
					foreach (Article article in kv.Value.FromPages.Select(title => new Article(PageTitle.Parse(title))))
					{
						yield return article;
					}
				}
			}
#endif

			foreach (var kv in creatorMapping)
			{
				if (kv.Value.MappedDeathyear != 9999
					|| !string.IsNullOrEmpty(kv.Value.MappedValue)
					|| !string.IsNullOrEmpty(kv.Value.MappedQID)
					|| kv.Value.IsUnknown)
				{
					//TODO: GetPages should automatically break up file lists
					foreach (Article article in kv.Value.FromPages.Select(title => new Article(PageTitle.Parse(title))))
					{
						yield return article;
					}
				}
			}
		}
	}
}
