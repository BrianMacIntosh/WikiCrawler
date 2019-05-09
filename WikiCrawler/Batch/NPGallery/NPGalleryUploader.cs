using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			"PhotoCredit",
			"Creator",
			"Publisher",
			"Constraints Information",
			"Attribution",
			"Create Date",
			"Content Dates",
			"Embedded Timestamp",
			"Location",
			"Categories",
			"Subject",
			"Description",
			"Image Details",
			"AltText",
			"Camera Information",
			"Exposure",
			"Focal Length",
			"ISO Speed",
			"Copyright",
			"Collector",

			// Soft use
			"Time Submitted",

			// Unused
			"Asset ID",
			"Related Collections",
			"Last Edited",
			"Original File Name",
			"File Name",
			"Resource Format",
			"File Size (bytes)",
			"Rating",
			"Original NPS Focus Uploader",
			"Source Information Description",
			"Source Location"
		};

		// List of copyright text blocks that can be ignored
		private static List<string> s_standardCopyrights = new List<string>()
		{
			"Permission must be secured from the individual copyright owners to reproduce any copyrighted materials contained within this website.",
			"Permission must be secured from the individual copyright owners to reproduce any copyrighted materials contained within this website. Digital assets without any copyright restrictions are public domain."
		};

		public NPGalleryUploader(string key)
			: base(key)
		{
			// load existing assetlist, only to count how many keys there are
			string assetlistFile = Path.Combine(ProjectDataDirectory, "assetlist.json");
			string json = File.ReadAllText(assetlistFile);
			List<NPGalleryAsset> allAssets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NPGalleryAsset>>(json);
			m_assetCount = allAssets.Count(asset => asset.AssetType == "Standard");
		}

		public override int TotalKeyCount
		{
			get { return m_assetCount; }
		}
		private int m_assetCount = -1;

		private const string ImageUriFormat = "https://npgallery.nps.gov/GetAsset/{0}/original.jpg";

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			// trim all metadata
			foreach (var kv in metadata.ToArray())
			{
				string value = kv.Value;
				value = value.Trim(StringUtility.WhitespaceExtended);
				value = value.TrimSingleEnclosingTags();
				value = value.Trim(StringUtility.WhitespaceExtended);
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

			List<string> categories = new List<string>();
			categories.Add(m_config.checkCategory);

			// check for different copyright text
			if (metadata.TryGetValue("Copyright", out outValue))
			{
				if (!s_standardCopyrights.Contains(outValue))
				{
					throw new Exception("Unrecognized copyright block");
				}
			}

			// determine the photographer
			string authorString = "";
			Creator creator = null;
			if (metadata.TryGetValue("Photographer", out outValue))
			{
				outValue = outValue.TrimEnd(" <i></i>");
				authorString = GetAuthor(outValue, "", out creator);
			}
			else if (metadata.TryGetValue("PhotoCredit", out outValue)
				&& !outValue.Contains(", Code: "))
			{
				outValue = outValue.TrimEnd(" <i></i>");
				authorString = GetAuthor(outValue, "", out creator);
			}
			else if (metadata.TryGetValue("Creator", out outValue)
				&& !outValue.Contains(", Code: "))
			{
				outValue = outValue.TrimEnd(" <i></i>");
				authorString = GetAuthor(outValue, "", out creator);
			}

			string publisher;
			metadata.TryGetValue("Publisher", out publisher);

			// determine the creation date
			DateParseMetadata createDateMetadata = DateParseMetadata.Unknown;
			string createDate = "";
			if (metadata.TryGetValue("Create Date", out outValue))
			{
				createDate = DateUtility.ParseDate(outValue, out createDateMetadata);
			}
			DateParseMetadata contentDateMetadata = DateParseMetadata.Unknown;
			string contentDate = "";
			if (metadata.TryGetValue("Content Dates", out outValue))
			{
				contentDate = DateUtility.ParseDate("Content Dates", out contentDateMetadata);
			}
			DateParseMetadata embeddedTimestampMetadata = DateParseMetadata.Unknown;
			string embeddedTimestamp = "";
			if (metadata.TryGetValue("Embedded Timestamp", out outValue))
			{
				embeddedTimestamp = DateUtility.ParseDate("Embedded Timestamp", out embeddedTimestampMetadata);
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
				string[] authorNames = authorString.TrimStart("NPS Photo /").Trim().Split(' ');
				if (authorNames.Length == 2 && NPSDirectoryQuery.QueryDirectory(authorNames[0], authorNames[1]))
				{
					licenseTag = "{{PD-USGov-NPS}}";
				}
			}

			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new LicenseException(dateMetadata.LatestYear, m_config.defaultPubCountry);
			}

			if (metadata.TryGetValue("Constraints Information", out outValue) && !outValue.Contains(", Code: "))
			{
				throw new Exception("'Constraints Information' contained something other than a park name");
			}
			else if (metadata.TryGetValue("Attribution", out outValue) && !outValue.Contains(", Code: "))
			{
				throw new Exception("'Attribution' contained something other than a park name");
			}

			// determine the park name
			string parkName = "";
			foreach (string metadataValue in metadata.Values)
			{
				int codeIndex = metadataValue.IndexOf(", Code: ");
				if (codeIndex >= 0)
				{
					parkName = metadataValue.Substring(0, codeIndex);
					break;
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
				otherFields += "\n{{Information field|name=Collector|value=" + outValue + "}}";
			}

			// need to download here to check EXIF
			CacheImage(key, metadata);
			Dictionary<string, FreeImageTag> exifData = ImageUtility.GetExif(GetImageCacheFilename(key));
			string photoInfo = "";
			if (!exifData.ContainsKey("FocalLength")
				&& !exifData.ContainsKey("ApertureValue")
				&& !exifData.ContainsKey("ExposureTime")
				&& !exifData.ContainsKey("ShutterSpeedValue")
				&& !exifData.ContainsKey("ISOSpeedRatings"))
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

			// get description
			List<string> descriptionBlocks = new List<string>();
			if (metadata.TryGetValue("AltText", out outValue))
			{
				descriptionBlocks.AddUnique(outValue);
			}
			if (metadata.TryGetValue("Image Details", out outValue))
			{
				descriptionBlocks.AddUnique(outValue);
			}
			if (metadata.TryGetValue("Description", out outValue))
			{
				descriptionBlocks.AddUnique(outValue);
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

			if (string.IsNullOrEmpty(authorString))
			{
				authorString = "{{unknown|author}}";
			}

			string page = GetCheckCategoriesTag(categories.Count) + "\n"
				+ "=={{int:filedesc}}==\n"
				+ "{{Photograph\n"
				+ "|photographer=" + authorString + "\n"
				+ "|publisher=" + publisher + "\n"
				+ "|title={{en|" + metadata["Title"] + "}}\n"
				+ "|description=\n"
				+ "{{en|" + description + "}}\n"
				+ "|depicted place=" + location + "\n"
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

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			foreach (string cat in categories)
			{
				page += "[[" + cat + "]]\n";
			}

			return page;
		}

		protected override void SaveOut()
		{
			base.SaveOut();

			NPSDirectoryQuery.SaveOut();
		}

		protected override string GetAuthor(string name, string lang, out Creator creator)
		{
			int italicStartIndex = name.IndexOf("<i>");
			if (italicStartIndex >= 0)
			{
				int italicEndIndex = name.IndexOf("</i>", italicStartIndex);
				string organization = name.SubstringRange(italicStartIndex + "<i>".Length, italicEndIndex - 1);
				name = name.Substring(italicStartIndex).TrimEnd();
			}

			return base.GetAuthor(name, lang, out creator);
		}

		protected override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			return new Uri(string.Format(ImageUriFormat, key));
		}

		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			return HttpUtility.HtmlDecode(metadata["Title"]) + " (" + key + ")";
		}
	}
}
