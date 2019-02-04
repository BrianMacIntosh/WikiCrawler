using System;
using System.Collections.Generic;

namespace UWash
{
	public class UWashDownloader : ContentDmDownloader
	{
		private string MetadataUrlFormat
		{
			get { return "http://digitalcollections.lib.washington.edu/cdm/singleitem/collection/" + UWashConfig.digitalCollectionsKey + "/id/{0}"; }
		}

		private string ImageUrlFormat
		{
			get
			{
				return "http://digitalcollections.lib.washington.edu/utils/ajaxhelper/?CISOROOT="
					+ UWashConfig.digitalCollectionsKey
					+ "&action=2&CISOPTR={0}&DMSCALE=100&DMWIDTH=99999&DMHEIGHT=99999&DMX=0&DMY=0&DMTEXT=&REC=1&DMTHUMB=0&DMROTATE=0";
			}
		}

		private UWashProjectConfig UWashConfig
		{
			get { return (UWashProjectConfig)m_config; }
		}

		public UWashDownloader(string key, UWashProjectConfig config)
			: base(key, config)
		{
			EasyWeb.SetDelayForDomain(new Uri(ImageUrlFormat), 15f);
		}

		/// <summary>
		/// Enumerates the list of all available item keys.
		/// </summary>
		protected override IEnumerable<string> GetKeys()
		{
			for (int index = UWashConfig.minIndex; index <= UWashConfig.maxIndex; index++)
			{
				yield return index.ToString();
			}
		}

		/// <summary>
		/// Returns the URL for the item with the specified key.
		/// </summary>
		protected override Uri GetItemUri(string key)
		{
			return new Uri(string.Format(MetadataUrlFormat, key));
		}
	}
}
