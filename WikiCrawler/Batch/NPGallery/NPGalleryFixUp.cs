using MediaWiki;
using System;
using System.Collections.Generic;

namespace NPGallery
{
	public class NPGalleryFixUp
	{
		protected Api Api = new Api(new Uri("https://commons.wikimedia.org/"));

		private NPGalleryDownloader m_downloader;
		private NPGalleryUploader m_uploader;

		public NPGalleryFixUp()
		{
			m_downloader = new NPGalleryDownloader("npgallery");
			m_downloader.HeartbeatEnabled = false;

			m_uploader = new NPGalleryUploader("npgallery");
		}

		public void Do()
		{
			Api.AutoLogIn();

			// fetch all files from the category
			foreach (Article article in Api.Search("insource:\"{{Information field|name=NPS Unit Code|value=LIBI}}\"", srnamespace: Api.BuildNamespaceList(Namespace.File)))
			{
				//TODO: only before 22:51, 20 July 2019

				Article fullArticle = Api.GetPage(article);
				string articleText = fullArticle.revisions[0].text;
				if (articleText.Contains("<gallery>"))
				{
					// already done
					continue;
				}
				else
				{
					// temporarily redownload metadata
					int lastParenIndex = article.title.LastIndexOf('(');
					if (lastParenIndex >= 0)
					{
						int closeParenIndex = article.title.IndexOf(')', lastParenIndex);
						string thisId = article.title.Substring(lastParenIndex + 1, closeParenIndex - lastParenIndex - 1);
						Dictionary<string, string> thisMetadata = m_downloader.Download(thisId, false);

						string relatedFlat;
						if (thisMetadata.TryGetValue("~Related", out relatedFlat))
						{
							string[] related = relatedFlat.Split('|');
							if (related.Length == 3)
							{
								/*int i = 0;
								string relatedId = related[i];
								string relatedType = related[i + 1];
								string relatedTitle = related[i + 2];
								Dictionary<string, string> relatedMetadata = m_uploader.ParseMetadata(relatedId);
								string uploadTitle = m_uploader.GetTitle(relatedId, relatedMetadata).Replace(s_badTitleCharacters, "");
								string imagePath = m_uploader.GetImageCacheFilename(relatedId, relatedMetadata);
								uploadTitle = Path.ChangeExtension(uploadTitle, Path.GetExtension(imagePath));
								otherVersions = StringUtility.Join("\n", otherVersions, uploadTitle);*/
							}
						}
					}
				}
			}
		}
	}
}
