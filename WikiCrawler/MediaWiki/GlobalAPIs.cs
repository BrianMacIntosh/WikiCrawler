using System;

namespace MediaWiki
{
	public static class GlobalAPIs
	{
		public static Api Commons
		{
			get
			{
				if (s_commons == null)
				{
					s_commons = new Api(new Uri("https://commons.wikimedia.org"));
					s_commons.AutoLogIn();
				}
				return s_commons;
			}
		}
		private static Api s_commons;

		public static Api Wikidata
		{
			get
			{
				if (s_wikidata == null)
				{
					s_wikidata = new Api(new Uri("https://www.wikidata.org"));
					s_wikidata.AutoLogIn();
				}
				return s_wikidata;
			}
		}
		private static Api s_wikidata;
	}
}
