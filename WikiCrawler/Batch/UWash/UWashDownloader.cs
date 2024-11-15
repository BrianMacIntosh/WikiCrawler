using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace UWash
{
	public class UWashDownloader : ContentDmDownloader
	{
		private string MetadataUrlFormat
		{
			//OLD ContentDM version
			//get { return "https://digitalcollections.lib.washington.edu/cdm/singleitem/collection/" + UWashConfig.digitalCollectionsKey + "/id/{0}"; }

			//NEW ContentDM version
			get { return "https://digitalcollections.lib.washington.edu/iiif/2/" + UWashConfig.digitalCollectionsKey + ":{0}/manifest.json"; }
		}

		private UWashProjectConfig UWashConfig
		{
			get { return (UWashProjectConfig)m_config; }
		}

		public UWashDownloader(string key)
			: base(key)
		{
			//HACK:
			m_config = JsonConvert.DeserializeObject<UWashProjectConfig>(
				File.ReadAllText(Path.Combine(ProjectDataDirectory, "config.json")));

			EasyWeb.SetDelayForDomain(new Uri(MetadataUrlFormat), 5f);
		}

		/// <summary>
		/// Enumerates the list of all available item keys.
		/// </summary>
		protected override IEnumerable<int> GetKeys()
		{
			for (int index = UWashConfig.minIndex; index <= UWashConfig.maxIndex; index++)
			{
				yield return index;
			}
		}

		/// <summary>
		/// Returns the URL for the item with the specified key.
		/// </summary>
		protected override Uri GetItemUri(int key)
		{
			return new Uri(string.Format(MetadataUrlFormat, key));
		}
	}
}
