using System;
using System.Collections.Generic;

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

		public static Api Wikipedia(string langCode)
		{
			langCode = langCode.ToLower();
			if (s_wikipedias.TryGetValue(langCode, out Api api))
			{
				return api;
			}
			else
			{
				Api apinew = new Api(new Uri(string.Format("https://{0}.wikipedia.org", langCode)));
				apinew.AutoLogIn();
				s_wikipedias[langCode] = apinew;
				return apinew;
			}
		}
		private static Dictionary<string, Api> s_wikipedias = new Dictionary<string, Api>();
	}
}
