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
			get { return "http://digitalcollections.lib.washington.edu/cdm/singleitem/collection/" + UWashConfig.digitalCollectionsKey + "/id/{0}"; }
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

			EasyWeb.SetDelayForDomain(new Uri(MetadataUrlFormat), 15f);
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
