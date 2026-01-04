using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using Tasks;

namespace NPGallery
{
	/// <summary>
	/// Remove anything from the data_cache that matches a succeeded or permafailed entry.
	/// </summary>
	public class NPGalleryCullCache : BatchTaskKeyed<Guid>
	{
		public NPGalleryCullCache()
			: base("npgallery")
		{
		}

		public override void Execute()
		{
			foreach (string file in Directory.GetFiles(MetadataCacheDirectory))
			{
				Guid key = StringToKey(Path.GetFileNameWithoutExtension(file));
				BatchItemStatus status = GetItemStatus(key);
				if (status == BatchItemStatus.Succeeded || status == BatchItemStatus.PermanentlySkipped)
				{
					Console.WriteLine("Trash " + file);
					File.Move(file, GetMetadataTrashFilename(key));
				}
			}
		}
	}

	/// <summary>
	/// Makes the cached data filenames consistent guids.
	/// </summary>
	public class NPGalleryGuidify : BatchTaskKeyed<Guid>
	{
		public NPGalleryGuidify()
			: base("npgallery")
		{

		}

		public override void Execute()
		{
			foreach (string file in Directory.GetFiles(MetadataCacheDirectory))
			{
				if (!File.Exists(file)) continue;

				Guid key = StringToKey(Path.GetFileNameWithoutExtension(file));
				string bestFileName = Path.Combine(Path.GetDirectoryName(file), key.ToString() + Path.GetExtension(file));
				if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(bestFileName), StringComparison.OrdinalIgnoreCase))
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
	}

	/// <summary>
	/// 
	/// </summary>
	public class NPGalleryAddRelated : BaseTask
	{
		protected static Guid StringToKey(string str)
		{
			return NPGallery.StringToKey(str);
		}

		public override void Execute()
		{
			NPGalleryDownloader downloader = new NPGalleryDownloader("npgallery");

			// fetch all files from the category
			foreach (Article article in GlobalAPIs.Commons.Search("insource:\"{{Information field|name=NPS Unit Code|value=LIBI}}\"", srnamespace: Api.BuildNamespaceList(Namespace.File)))
			{
				//TODO: only before 22:51, 20 July 2019

				Article fullArticle = GlobalAPIs.Commons.GetPage(article);
				string articleText = fullArticle.revisions[0].text;
				if (articleText.Contains("<gallery>"))
				{
					// already done
					continue;
				}
				else
				{
					// temporarily redownload metadata
					int lastParenIndex = article.title.Name.LastIndexOf('(');
					if (lastParenIndex >= 0)
					{
						int closeParenIndex = article.title.Name.IndexOf(')', lastParenIndex);
						string thisId = article.title.Name.Substring(lastParenIndex + 1, closeParenIndex - lastParenIndex - 1);
						Guid thisKey = StringToKey(thisId);
						Dictionary<string, string> thisMetadata = downloader.Download(thisKey, false);

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
