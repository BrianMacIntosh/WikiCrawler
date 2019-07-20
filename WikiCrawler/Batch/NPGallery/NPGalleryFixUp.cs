using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wikimedia;

namespace NPGallery
{
	public class NPGalleryFixUp
	{
		protected WikiApi Api = new WikiApi(new Uri("https://commons.wikimedia.org/"));

		private NPGalleryDownloader m_downloader;

		public NPGalleryFixUp()
		{
			m_downloader = new NPGalleryDownloader("npgallery");
			m_downloader.HeartbeatEnabled = false;
		}

		public void Do()
		{
			Api.LogIn();

			// fetch all files from the category
			//Api.SearchEntities("Category:Images from NPGallery")
		}
	}
}
