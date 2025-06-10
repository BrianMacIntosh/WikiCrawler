using MediaWiki;
using System;
using WikiCrawler;

namespace Tasks
{
	public class WikidataCacheTest : BaseTask
	{
		public override void Execute()
		{
			QId testQid = new QId(20504742);
			WikidataCache.InvalidateArtwork(testQid);
			ArtworkData data = WikidataCache.GetArtworkData(testQid);
			Console.WriteLine(data.CreatorQid);
			Console.WriteLine(data.LatestYear);
		}
	}
}
