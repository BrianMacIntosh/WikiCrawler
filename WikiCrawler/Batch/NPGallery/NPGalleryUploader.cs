using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using WikiCrawler;

namespace NPGallery
{
	public class NPGalleryUploader : BatchUploader
	{
		private HashSet<string> m_usedKeys = new HashSet<string>()
		{
			"Title",
			"Photographer",
			"Photographer, attributed",
			"PhotoCredit",
			"Creator",
			"Publisher",
			"Compiler",
			"Constraints Information",
			"Attribution",
			"Locations",
			"Create Date",
			"Date Created",
			"Content Dates",
			"Embedded Timestamp",
			"Categories",
			"Keywords",
			"Controlled Keywords",
			"Subject",
			"Description",
			"Image Details",
			"Comment",
			"AltText",
			"Camera Information",
			"Exposure",
			"Focal Length",
			"ISO Speed",
			"Copyright",
			"Collector",
			"Editor",
			"Sponsor",
			"Cataloguer",
			"Contacts",
			"Related System IDs",
			"NPS Units", // implicit use by the bit that iterates all fields looking for "Code:"
			"~Related",

			// Soft use
			"Time Submitted",
			"Intended Audience",
			"~Version",

			// Unused
			"Asset ID",
			"XmpMetadataDate", // date of metadata update, not that useful
			"Related Collections",
			"Last Edited",
			"Original File Name",
			"File Name",
			"Resource Format",
			"Resource Type",
			"File Size (bytes)",
			"Rating",
			"Related Resources", //facebook page
			"Related Portals",
			"Original NPS Focus Uploader",
			"NPGallery Uploader",
			"Source Information Description",
			"Source Location",
			"Albums", // Obsolete
		};

		// List of copyright text blocks that can be ignored
		private static List<string> s_standardCopyrights = new List<string>()
		{
			"Permission must be secured from the individual copyright owners to reproduce any copyrighted materials contained within this website.",
			"Permission must be secured from the individual copyright owners to reproduce any copyrighted materials contained within this website. Digital assets without any copyright restrictions are public domain.",
			"This digital asset is provided for &#39;fair use&#39; purposes. The National Park Service is not necessarily the holder of the original copyright and is not legally liable for infringement when materials are wrongfully used.",

			//TODO: make a license to use with this
			"To the best of our knowledge we believe this image to by copyright free and in the public domain."
		};

		private NPGalleryDownloader m_downloader;

		public NPGalleryUploader(string key)
			: base(key)
		{
			// load existing assetlist, only to count how many keys there are
			string assetlistFile = Path.Combine(ProjectDataDirectory, "assetlist.json");
			string json = File.ReadAllText(assetlistFile);
			List<NPGalleryAsset> allAssets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NPGalleryAsset>>(json);
			m_assetCount = allAssets.Count(asset => asset.AssetType == "Standard");

			// we'll need to redownload data when the Albums key is missing
			m_downloader = new NPGalleryDownloader(key);
			m_downloader.HeartbeatEnabled = false;
		}

		public static string[] KeywordSplitters = { "<br/>", "<br />", "<br>", "\r", "\n", "," };

		public override int TotalKeyCount
		{
			get { return m_assetCount; }
		}
		private int m_assetCount = -1;

		private const string ImageUriFormat = "https://npgallery.nps.gov/GetAsset/{0}/";

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			// trim all metadata
			foreach (var kv in metadata.ToArray())
			{
				string value = kv.Value;
				value = value.Trim(StringUtility.WhitespaceExtended);
				value = value.TrimSingleEnclosingTags();
				value = value.TrimEnd("<i></i>");
				value = value.Trim(StringUtility.WhitespaceExtended);
				value = WebUtility.HtmlDecode(value);
				metadata[kv.Key] = value;
			}

			// make sure all keys are used
			foreach (string dataKey in metadata.Keys)
			{
				if (!m_usedKeys.Contains(dataKey))
				{
					throw new Exception("Unused key '" + dataKey + "'.");
				}
			}

			string outValue;

			HashSet<string> categories = new HashSet<string>();

			if (metadata.TryGetValue("Intended Audience", out outValue))
			{
				if (outValue != "Public")
				{
					throw new Exception("Unrecognized Intended Audience");
				}
			}

			// check for different copyright text
			if (metadata.TryGetValue("Copyright", out outValue))
			{
				if (!s_standardCopyrights.Contains(outValue))
				{
					//TODO: make use of this info
					if (!outValue.StartsWith("This digital asset is in the public domain."))
					{
						throw new Exception("Unrecognized copyright block");
					}
				}
			}

			// determine the photographer
			string authorString = "";
			Creator creator = null;
			if (metadata.TryGetValue("Photographer", out outValue))
			{
				authorString = GetAuthor(outValue, "", out creator);
			}
			else if (metadata.TryGetValue("Photographer, attributed", out outValue))
			{
				authorString = GetAuthor(outValue, "", out creator);
			}
			else if (metadata.TryGetValue("PhotoCredit", out outValue)
				&& !outValue.Contains(", Code: "))
			{
				authorString = GetAuthor(outValue, "", out creator);
			}
			else if (metadata.TryGetValue("Creator", out outValue)
				&& !outValue.Contains(", Code: "))
			{
				authorString = GetAuthor(outValue, "", out creator);
			}

			if (creator != null)
			{
				//HACK;
				creator.UploadableUsage++;
			}

			string publisher;
			metadata.TryGetValue("Publisher", out publisher);

			// determine the creation date
			DateParseMetadata createDateMetadata = DateParseMetadata.Unknown;
			string createDate = "";
			if (metadata.TryGetValue("Create Date", out outValue))
			{
				createDate = DateUtility.ParseDate(outValue, out createDateMetadata, true);
			}
			DateParseMetadata dateCreatedMetadata = DateParseMetadata.Unknown;
			string dateCreated = "";
			if (metadata.TryGetValue("Date Created", out outValue))
			{
				dateCreated = DateUtility.ParseDate(outValue, out dateCreatedMetadata, true);
			}
			DateParseMetadata contentDateMetadata = DateParseMetadata.Unknown;
			string contentDate = "";
			if (metadata.TryGetValue("Content Dates", out outValue))
			{
				contentDate = DateUtility.ParseDate(outValue, out contentDateMetadata, true);
			}
			DateParseMetadata embeddedTimestampMetadata = DateParseMetadata.Unknown;
			string embeddedTimestamp = "";
			if (metadata.TryGetValue("Embedded Timestamp", out outValue))
			{
				embeddedTimestamp = DateUtility.ParseDate(outValue, out embeddedTimestampMetadata, true);
			}
			DateParseMetadata dateMetadata = DateParseMetadata.Unknown;
			string date = "";
			if (contentDateMetadata != DateParseMetadata.Unknown)
			{
				dateMetadata = contentDateMetadata;
				date = contentDate;
			}
			else if (createDateMetadata != DateParseMetadata.Unknown)
			{
				dateMetadata = createDateMetadata;
				date = createDate;
			}
			else if (dateCreatedMetadata != DateParseMetadata.Unknown)
			{
				dateMetadata = dateCreatedMetadata;
				date = dateCreated;
			}
			else if (embeddedTimestampMetadata != DateParseMetadata.Unknown)
			{
				dateMetadata = embeddedTimestampMetadata;
				date = embeddedTimestamp;
			}
			if (metadata.TryGetValue("Time Submitted", out outValue))
			{
				DateTime timeSubmitted = DateTime.Parse(outValue);
				if (timeSubmitted.Year < dateMetadata.PreciseYear)
				{
					throw new Exception("Time Submitted is before than Create/Content Date(s)");
				}
			}

			// if the date has an accurate month, day, and year, use {{taken on|}}
			if (m_config.informationTemplate == "Photograph"
				&& DateUtility.IsExactDateModern(date)
				// these are too frequently inaccurate
				&& !date.EndsWith("-01-01") && !date.EndsWith("-1-1"))
			{
				date = "{{taken on|" + date + "}}";
			}

			string licenseTag = GetLicenseTag(authorString, creator, dateMetadata.LatestYear, m_config.defaultPubCountry);

			if (string.IsNullOrEmpty(licenseTag))
			{
				string[] authorNames = authorString.TrimStart("NPS Photo /").TrimStart("NPS/").Trim().Split(' ');
				/*if (authorNames.Length == 2 && NPSDirectoryQuery.QueryDirectory(authorNames[0], authorNames[1]))
				{
					licenseTag = "{{PD-USGov-NPS}}";
				}*/
			}

			//TODO: separate alt text

			// get description
			List<string> descriptionBlocks = new List<string>();
			bool needScopeCheck = false;
			if (metadata.TryGetValue("AltText", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue) || needScopeCheck;
			}
			if (metadata.TryGetValue("Image Details", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue) || needScopeCheck;
			}
			if (metadata.TryGetValue("Description", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue) || needScopeCheck;
			}
			if (metadata.TryGetValue("Comment", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue) || needScopeCheck;
			}
			if (metadata.TryGetValue("Subject", out outValue))
			{
				descriptionBlocks.AddUnique("*Subject: " + outValue);
			}
			string description;
			if (descriptionBlocks.Count == 0)
			{
				description = metadata["Title"];
			}
			else if (descriptionBlocks.Count == 1)
			{
				description = descriptionBlocks[0];
			}
			else
			{
				description = "<p>" + string.Join("</p>\n<p>", descriptionBlocks) + "</p>";
			}

			if (needScopeCheck)
			{
				//categories.Add("Category:Images from NPGallery to check for scope");
				throw new Exception("Out of scope?");
			}

			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new LicenseException(dateMetadata.LatestYear, m_config.defaultPubCountry);
			}

			if (metadata.TryGetValue("Constraints Information", out outValue))
			{
				if (!outValue.Contains(", Code: ")
					&& outValue != "Public domain"
					&& outValue != "Public domain:Full Granting Rights") //TODO: use this
				{
					throw new Exception("'Constraints Information' contained something other than a park name");
				}
			}
			else if (metadata.TryGetValue("Attribution", out outValue) && !outValue.Contains(", Code: "))
			{
				throw new Exception("'Attribution' contained something other than a park name");
			}

			// determine the park name
			string parkName = "";
			List<string> parkCodes = new List<string>();
			foreach (string metadataValue in metadata.Values)
			{
				foreach (string park in metadataValue.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries))
				{
					int codeIndex = park.IndexOf(", Code: ");
					if (codeIndex >= 0)
					{
						parkName = park.Substring(0, codeIndex);
						parkCodes.Add(park.Substring(codeIndex + ", Code: ".Length));
					}
				}
			}
			if (!string.IsNullOrEmpty(parkName))
			{
				foreach (string category in CategoryTranslation.TranslateLocationCategory(parkName))
				{
					string definiteLocation = category.Substring("Category:".Length);

					if (dateMetadata.PreciseYear != 0)
					{
						string yearLocCat = "Category:" + dateMetadata.PreciseYear.ToString() + " in " + definiteLocation;
						Wikimedia.Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
						if (existingYearLocCat != null)
						{
							categories.Add(existingYearLocCat.title);
						}
						else
						{
							categories.Add(category);
						}
					}
					else
					{
						categories.Add(category);
					}
				}
			}

			// determine the location
			string location = parkName;

			if (metadata.TryGetValue("Locations", out outValue))
			{
				string[] locationSplit = outValue.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries);
				if (locationSplit.Length > 2)
				{
					throw new Exception("Unrecognized Locations format");
				}
				location = locationSplit[0];
			}

			if (metadata.TryGetValue("Categories", out outValue))
			{
				foreach (string dataSplit in outValue.Split(','))
				{
					string subject = dataSplit.Trim();
					if (subject == "Scenic" | subject == "Historic")
					{
						// not useful
						continue;
					}
					IEnumerable<string> mappedCats = CategoryTranslation.TranslateTagCategory(subject);
					if (mappedCats != null)
					{
						categories.AddRange(mappedCats);
					}
				}
			}
			string keywordsToParse = "";
			if (metadata.TryGetValue("Keywords", out outValue))
			{
				keywordsToParse = StringUtility.Join(", ", keywordsToParse, outValue);
			}
			if (metadata.TryGetValue("Controlled Keywords", out outValue))
			{
				keywordsToParse = StringUtility.Join(", ", keywordsToParse, outValue);
			}
			if (!string.IsNullOrEmpty(keywordsToParse))
			{
				keywordsToParse = StringUtility.CleanHtml(keywordsToParse);
				List<string> parsedKeywords = new List<string>();
				foreach (string dataSplit in keywordsToParse.Split(KeywordSplitters, StringSplitOptions.RemoveEmptyEntries))
				{
					// strip links
					string dataTrimmed = dataSplit.Trim();

					if (string.IsNullOrEmpty(dataTrimmed) || dataTrimmed == "()")
					{
						continue;
					}

					// park codes will not map to meaningful categories
					if (!parkCodes.Contains(dataTrimmed, StringComparer.CurrentCultureIgnoreCase))
					{
						IEnumerable<string> mappedCats = CategoryTranslation.TranslateTagCategory(dataTrimmed);
						if (mappedCats != null)
						{
							categories.AddRange(mappedCats);
						}
					}

					if (!parsedKeywords.Contains(dataTrimmed, StringComparer.CurrentCultureIgnoreCase))
					{
						parsedKeywords.Add(dataTrimmed);
					}
				}
				if (parsedKeywords.Count > 0)
				{
					description += "\n*Keywords: " + string.Join("; ", parsedKeywords);
				}
			}
			if (metadata.TryGetValue("Subject", out outValue))
			{
				foreach (string dataSplit in outValue.Split(','))
				{
					string subject = dataSplit.Trim();
					IEnumerable<string> mappedCats = CategoryTranslation.TranslateTagCategory(subject);
					if (mappedCats != null)
					{
						categories.AddRange(mappedCats);
					}
				}
			}

			if (creator != null && !string.IsNullOrEmpty(creator.Category))
			{
				categories.Add(creator.Category);
			}

			string otherFields = "";
			if (metadata.TryGetValue("Collector", out outValue))
			{
				otherFields += "\n{{Information field|name=Collector|value={{en|" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Editor", out outValue))
			{
				otherFields += "\n{{Information field|name=Editor|value={{en|" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Sponsor", out outValue))
			{
				otherFields += "\n{{Information field|name=Sponsor|value={{en|" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Cataloguer", out outValue))
			{
				otherFields += "\n{{Information field|name=Cataloguer|value={{en|" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Contacts", out outValue))
			{
				otherFields += "\n{{Information field|name=Contacts|value={{en|" + outValue + "}}}}";
			}
			if (parkCodes.Count > 0)
			{
				otherFields += "\n{{Information field|name=NPS Unit Code|value=" + string.Join(", ", parkCodes) + "}}";
			}
			if (metadata.TryGetValue("Compiler", out outValue))
			{
				otherFields += "\n{{Information field|name=Compiler|value={{en|" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Related System IDs", out outValue))
			{
				string[] relatedIDs = outValue.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries);
				foreach (string line in relatedIDs)
				{
					string[] lineSplit = line.Split(new char[] { ':' }, 2);
					if (lineSplit.Length != 2)
					{
						throw new Exception("Unhandled format in Related System IDs");
					}
					string systemKey = StringUtility.CleanHtml(lineSplit[0]).Trim();
					string systemValue = StringUtility.CleanHtml(lineSplit[1]).Trim();
					if (systemValue.StartsWith("Mission 2000 record no."))
					{
						throw new Exception("Document");
					}
					otherFields += "\n{{Information field|name=" + systemKey  + "|value=" + systemValue + "}}";
				}
			}
			
			if (metadata.TryGetValue("~Related", out outValue) && !string.IsNullOrEmpty(outValue))
			{
				// album titles might work as categories
				string[] related = outValue.Split('|');
				List<string> relatedAlbums = new List<string>();
				for (int i = 0; i < related.Length; i += 3)
				{
					//TODO: do something with related assets?

					string relatedId = related[i];
					string relatedType = related[i + 1];
					string relatedTitle = related[i + 2];
					if (relatedType == "Album")
					{
						relatedAlbums.Add(relatedTitle);

						IEnumerable<string> mappedCats = CategoryTranslation.TranslateTagCategory(relatedTitle);
						if (mappedCats != null)
						{
							categories.AddRange(mappedCats);
						}
					}
				}

				if (relatedAlbums.Count > 0)
				{
					otherFields += "\n{{Information field|name=Album(s)|value={{en|" + string.Join("; ", relatedAlbums) + "}}}}";
				}
			}

			// need to download here to check EXIF
			CacheImage(key, metadata);
			Dictionary<string, FreeImageTag> exifData = ImageUtility.GetExif(GetImageCacheFilename(key, metadata));
			string photoInfo = "";
			if (!exifData.ContainsKey("FocalLength")
				&& !exifData.ContainsKey("ApertureValue")
				&& !exifData.ContainsKey("ExposureTime")
				&& !exifData.ContainsKey("ShutterSpeedValue")
				&& !exifData.ContainsKey("ISOSpeedRatings")
				&& !exifData.ContainsKey("Model"))
			{
				// include camera info if available and not in exif
				if (metadata.TryGetValue("Camera Information", out outValue))
				{
					photoInfo += "|Model=" + outValue + "\n";
				}
				if (metadata.TryGetValue("Exposure", out outValue))
				{
					string[] split = outValue.Split(" sec at ", StringSplitOptions.None);
					if (split.Length != 2) throw new Exception("Could not parse 'Exposure'");
					photoInfo += "|Shutter=" + split[0];
					photoInfo += "|Aperture=" + split[1];
				}
				if (metadata.TryGetValue("Focal Length", out outValue))
				{
					photoInfo += "|Focal length=" + outValue + "\n";
				}
				if (metadata.TryGetValue("ISO Speed", out outValue))
				{
					outValue = outValue.TrimStart("ISO").Trim();
					photoInfo += "|ISO=" + outValue + "\n";
				}
			}

			if (string.IsNullOrEmpty(authorString))
			{
				authorString = "{{unknown|author}}";
			}
			else if (!authorString.StartsWith("{{"))
			{
				authorString = "{{en|" + authorString + "}}";
			}

			if (categories.Contains("Category:Graves") && parkCodes.Contains("MACA"))
			{
				throw new Exception("Gravestone");
			}

			// now that this looks like a success, redownload and start over
			string version;
			if (!metadata.TryGetValue("~Version", out version) || int.Parse(version) < NPGalleryDownloader.Version)
			{
				Console.WriteLine("Old version, redownloading");
				metadata = m_downloader.Download(key);
				return BuildPage(key, metadata);
			}

			//TODO: captions

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			string catCheckTag = GetCheckCategoriesTag(categories.Count);
			categories.Add(m_config.checkCategory);

			string page = catCheckTag + "\n"
				+ "=={{int:filedesc}}==\n"
				+ "{{Photograph\n"
				+ "|photographer=" + authorString + "\n";
			if (!string.IsNullOrEmpty(publisher))
			{
				page += "|publisher={{en|" + publisher + "}}\n";
			}
			page += "|title={{en|" + metadata["Title"] + "}}\n"
				+ "|description=\n"
				+ "{{en|" + description + "}}\n"
				+ "|depicted place={{en|" + location + "}}\n"
				+ "|date=" + date + "\n"
				+ "|accession number={{NPGallery-accession|" + key + "}}\n"
				+ "|source=" + m_config.sourceTemplate + "\n"
				+ "|permission=" + licenseTag + "\n"
				+ "|other_versions=\n"
				+ "|other_fields=" + otherFields + "\n";
			if (!string.IsNullOrEmpty(photoInfo))
			{
				page += "{{Photo Information" + photoInfo + "}}\n";
			}
			page += "}}\n";

			foreach (string cat in categories)
			{
				page += "[[" + cat + "]]\n";
			}

			return page;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>False if the image needs a scope check</returns>
		private bool AddDescriptionBlock(List<string> descriptionBlocks, string descriptionBlock)
		{
			descriptionBlock = descriptionBlock.RemoveIndentation().RemoveExtraneousLines();

			descriptionBlocks.AddUnique(descriptionBlock);

			// check for FOP issues
			if (descriptionBlock.Contains("statue "))
			{
				throw new Exception("Possible FOP issue");
			}

			// add an additional check category to possible out-of-scope photos
			string[] split = descriptionBlock.ToLower().Split(' ');
			if (split[0] == "a" || split[0] == "one"
				|| split[0] == "two" || split[0] == "three"
				|| split[0] == "four" || split[0] == "five"
				|| split[0] == "six" || split[0] == "seven"
				|| split[0] == "eight" || split[0] == "nine"
				|| split[0] == "ten")
			{
				if (split[1] == "person" || split[1]  == "people"
					//|| split[1] == "man" || split[1] == "men"
					//|| split[1] == "woman" || split[1] == "women"
					|| split[1] == "hiker" || split[1] == "hikers"
					|| split[1] == "camper" || split[1] == "campers"
					|| split[1] == "visitor" || split[1] == "visitors"
					|| split[1] == "young child" || split[1] == "young children"
					|| split[1] == "child" || split[1] == "children"
					|| split[1] == "kid" || split[1] == "kids"
					|| split[1] == "teenager" || split[1] == "teenagers")
				{
					return false;
				}
			}

			if (descriptionBlock.StartsWith("people ")
				|| descriptionBlock.StartsWith("person ")
				|| descriptionBlock.StartsWith("woman "))
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		protected override void SaveOut()
		{
			base.SaveOut();

			NPSDirectoryQuery.SaveOut();
		}

		protected override string GetAuthor(string name, string lang, out Creator creator)
		{
			string organization = "";
			int italicStartIndex = name.IndexOf("<i>");
			if (italicStartIndex >= 0)
			{
				int italicEndIndex = name.IndexOf("</i>", italicStartIndex);
				organization = name.SubstringRange(italicStartIndex + "<i>".Length, italicEndIndex - 1);
				name = name.Substring(0, italicStartIndex - 1).TrimEnd();
			}

			string niceAuthor = base.GetAuthor(name, lang, out creator);
			if (!string.IsNullOrEmpty(organization))
			{
				niceAuthor += " (''" + organization + "'')";
			}
			return niceAuthor;
		}

		protected override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			string url = string.Format(ImageUriFormat, key) + "original" + GetImageExtension(key, metadata);
			return new Uri(url);
		}

		protected override string GetImageCacheFilename(string key, Dictionary<string, string> metadata)
		{
			return Path.ChangeExtension(base.GetImageCacheFilename(key, metadata), GetImageExtension(key, metadata));
		}

		protected override string GetImageCroppedFilename(string key, Dictionary<string, string> metadata)
		{
			return Path.ChangeExtension(base.GetImageCroppedFilename(key, metadata), GetImageExtension(key, metadata));
		}

		private string GetImageExtension(string key, Dictionary<string, string> metadata)
		{
			string originalFileName;
			if (metadata.TryGetValue("Original File Name", out originalFileName))
			{
				return Path.GetExtension(originalFileName);
			}
			else
			{
				return ".jpg";
			}
		}

		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			return HttpUtility.HtmlDecode(metadata["Title"]) + " (" + key + ")";
		}
	}
}
