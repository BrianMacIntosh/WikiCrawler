using MediaWiki;
using System.Collections.Generic;

namespace Tasks
{
	public class PdArtFollowup : ReplaceIn
	{
		public PdArtFollowup()
			: base(PdArtFixup.CreateReplacement())
		{
		}

		public override IEnumerable<Article> GetFilesToAffectUncached(string startSortkey)
		{
			ManualMapping<MappingCreator> creatorMapping = new ManualMapping<MappingCreator>(ImplicitCreatorsReplacement.CreatorMappingFile);
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
