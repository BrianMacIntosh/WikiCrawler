using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;

namespace NPGallery
{
	public class NPGalleryFixUp : BatchTaskKeyed<Guid>
	{
		protected Api Api = new Api(new Uri("https://commons.wikimedia.org/"));

		private NPGalleryDownloader m_downloader;
		private NPGalleryUploader m_uploader;

		public NPGalleryFixUp()
			: base("npgallery")
		{
			//m_downloader = new NPGalleryDownloader("npgallery");
			//m_downloader.HeartbeatEnabled = false;

			//m_uploader = new NPGalleryUploader("npgallery");
		}

		protected override Guid StringToKey(string str)
		{
			return NPGallery.StringToKey(str);
		}

		public void Do()
		{
			Api.AutoLogIn();

			DoAddRelated();

			SaveOut();
		}

		private void DoRetroactivePermanentSkip()
		{
			foreach (string imageFile in Directory.GetFiles(ImageCacheDirectory))
			{

			}
		}

		public void DoCullCache()
		{
			// remove anything from the data_cache that matches a succeeded or permafailed entry
			foreach (string file in Directory.GetFiles(MetadataCacheDirectory))
			{
				Guid key = StringToKey(Path.GetFileNameWithoutExtension(file));
				if (m_succeeded.Contains(key) || m_permanentlyFailed.Contains(key))
				{
					File.Delete(file);
					Console.WriteLine("Delete " + file);
				}
			}
		}

		public void GuidifyFileNames()
		{
			foreach (string file in Directory.GetFiles(MetadataCacheDirectory))
			{
				if (!File.Exists(file)) continue;

				Guid key = StringToKey(Path.GetFileNameWithoutExtension(file));
				string bestFileName = Path.Combine(Path.GetDirectoryName(file), key.ToString() + Path.GetExtension(file));
				if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(bestFileName), StringComparison.InvariantCultureIgnoreCase))
				{
					if (File.Exists(bestFileName))
					{
						// keep newest
						if (File.GetCreationTime(bestFileName) > File.GetCreationTime(file))
						{
							File.Move(file, GetMetadataTrashFilename(key));
							Console.WriteLine("Delete {0}", file);
						}
						else
						{
							File.Move(bestFileName, GetMetadataTrashFilename(key));
							Console.WriteLine("Delete {0}", bestFileName);
						}
					}
					else
					{
						File.Move(file, bestFileName);
						Console.WriteLine("{0} -> {1}", file, bestFileName);
					}
				}
			}
		}

		private void DoAddRelated()
		{
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
						Guid thisKey = StringToKey(thisId);
						Dictionary<string, string> thisMetadata = m_downloader.Download(thisKey, false);

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
