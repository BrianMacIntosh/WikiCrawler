using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Tasks.Commons;
using WikiCrawler;

namespace NPGallery
{
	public class NPGalleryUploader : BatchUploader<Guid>
	{
		private HashSet<string> m_usedKeys = new HashSet<string>()
		{
			"Title",
			"Photographer",
			"Author",
			"Photographer, attributed",
			"Creator, attributed",
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
			"Table Of Contents",
			"Comment",
			"AltText",
			"Online Links to Resource",
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
			"uploader",
			"NPGallery Uploader",
			"Source Information Description",
			"Source Location",
			"Access Constraints",
			"Albums", // Obsolete
		};

		// List of copyright text blocks that can be ignored
		private static List<string> s_standardCopyrights = new List<string>()
		{
			"Permission must be secured from the individual copyright owners to reproduce any copyrighted materials contained within this website.",
			"Permission must be secured from the individual copyright owners to reproduce any copyrighted materials contained within this website. Digital assets without any copyright restrictions are public domain.",
			"This digital asset is provided for 'fair use' purposes. The National Park Service is not necessarily the holder of the original copyright and is not legally liable for infringement when materials are wrongfully used.",
			"To the best of our knowledge we believe this image to by copyright free and in the public domain.",

			"National Park Service",
			"NPS Photo",
			"NPS"
		};

		private NPGalleryDownloader m_downloader;
		private readonly SQLiteConnection AssetsDatabase;
		private readonly SQLiteCommand ShouldAttemptKeyQuery;
		private readonly SQLiteCommand MarkAttemptQuery;

		/// <summary>
		/// The last Unix time at which a change was made that requires all files to recheck to reupload.
		/// </summary>
		private const int lastUploadChangeTime = 0;

		public NPGalleryUploader(string key)
			: base(key)
		{
			SQLiteConnectionStringBuilder connectionString = new SQLiteConnectionStringBuilder
			{
				{ "Data Source", NPGallery.AssetDatabaseFile },
				{ "Mode", "ReadOnly" }
			};
			AssetsDatabase = new SQLiteConnection(connectionString.ConnectionString);
			AssetsDatabase.Open();

			ShouldAttemptKeyQuery = AssetsDatabase.CreateCommand();
			ShouldAttemptKeyQuery.CommandText = $"SELECT last_upload_attempt IS NULL OR last_upload_attempt<{lastUploadChangeTime} FROM assets WHERE id=$id";

			MarkAttemptQuery = AssetsDatabase.CreateCommand();
			MarkAttemptQuery.CommandText = $"UPDATE assets SET last_upload_attempt=unixepoch() WHERE id=$id";

			SQLiteCommand command = AssetsDatabase.CreateCommand();
			command.CommandText = "SELECT COUNT(id) FROM assets WHERE assettype=\"Standard\" or assettype=\"Standard File\"";
			object assetCountQueryResult = command.ExecuteScalar();
			m_assetCount = (int)(long)assetCountQueryResult;

			// we'll need to redownload data when the Albums key is missing
			m_downloader = new NPGalleryDownloader(key);

			// get the hash of some error files
			using (MD5 md5 = MD5.Create())
			{
				using (FileStream stream = File.OpenRead(Path.Combine(ProjectDataDirectory, "file-system-error.png")))
				{
					m_fileSystemErrorHash = md5.ComputeHash(stream);
				}
				using (FileStream stream = File.OpenRead(Path.Combine(ProjectDataDirectory, "database-error.png")))
				{
					m_databaseErrorHash = md5.ComputeHash(stream);
				}
				using (FileStream stream = File.OpenRead(Path.Combine(ProjectDataDirectory, "corrupted-record.pdf")))
				{
					m_corruptedRecordErrorHash = md5.ComputeHash(stream);
				}
				using (FileStream stream = File.OpenRead(Path.Combine(ProjectDataDirectory, "not-authorized.png")))
				{
					m_notAuthorizedHashes.Add(md5.ComputeHash(stream));
				}
				using (FileStream stream = File.OpenRead(Path.Combine(ProjectDataDirectory, "not-authorized.tif")))
				{
					m_notAuthorizedHashes.Add(md5.ComputeHash(stream));
				}
				using (FileStream stream = File.OpenRead(Path.Combine(ProjectDataDirectory, "not-authorized.jpg")))
				{
					m_notAuthorizedHashes.Add(md5.ComputeHash(stream));
				}
			}

			// read unit code locations
			string unitCodeLocsPath = Path.Combine(Configuration.DataDirectory, "npsunit-parents.json");
			string unitCodeLocsText = File.ReadAllText(unitCodeLocsPath, Encoding.UTF8);
			s_unitCodeToCommonsLoc = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(unitCodeLocsText);
		}

		protected override Guid StringToKey(string str)
		{
			return NPGallery.StringToKey(str);
		}

		/// <summary>
		/// Maps NPS unit codes to commons location categories.
		/// </summary>
		private static Dictionary<string, string[]> s_unitCodeToCommonsLoc = new Dictionary<string, string[]>();
		
		public static string[] KeywordSplitters = { "<br/>", "<br />", "<br>", "\r", "\n", "," };

		private byte[] m_fileSystemErrorHash;
		private byte[] m_databaseErrorHash;
		private byte[] m_corruptedRecordErrorHash;
		private List<byte[]> m_notAuthorizedHashes = new List<byte[]>();

		public override int TotalKeyCount
		{
			get { return m_assetCount; }
		}
		private int m_assetCount = -1;

		private const string ImageUriFormat = "https://npgallery.nps.gov/GetAsset/{0}/";

		public override bool ShouldAttemptKey(Guid key)
		{
			ShouldAttemptKeyQuery.Parameters.AddWithValue("id", key.ToString("N"));
			return (long)ShouldAttemptKeyQuery.ExecuteScalar() != 0;
		}

		protected override string BuildPage(Guid key, Dictionary<string, string> metadata)
		{
			MarkAttemptQuery.Parameters.AddWithValue("id", key.ToString("N"));
			MarkAttemptQuery.ExecuteNonQuery();

			// trim all metadata
			foreach (KeyValuePair<string, string> kv in metadata.ToArray())
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
			/*foreach (string dataKey in metadata.Keys)
			{
				if (!m_usedKeys.Contains(dataKey))
				{
					throw new Exception("Unused key '" + dataKey + "'.");
				}
			}*/

			string infoTemplate = m_config.informationTemplate;

			string outValue;

			HashSet<PageTitle> categories = new HashSet<PageTitle>();

			/*if (metadata.TryGetValue("Intended Audience", out outValue))
			{
				if (outValue != "Public")
				{
					throw new Exception("Unrecognized Intended Audience");
				}
			}*/

			// check for different copyright text
			/*if (metadata.TryGetValue("Copyright", out outValue))
			{
				if (!s_standardCopyrights.Contains(outValue)
					&& !outValue.StartsWith("NPS Photo /"))
				{
					//TODO: make use of this info
					if (!outValue.StartsWith("This digital asset is in the public domain."))
					{
						throw new Exception("Unrecognized copyright: '" + outValue + "'");
					}
				}
			}*/

			// determine the photographer
			string authorString = "";
			List<MappingCreator> creators = null;
			if (metadata.TryGetValue("Photographer", out outValue))
			{
				authorString = GetAuthor(key.ToString(), outValue, "", ref creators);
			}
			else if (metadata.TryGetValue("Photographer, attributed", out outValue))
			{
				authorString = GetAuthor(key.ToString(), outValue, "", ref creators);
			}
			else if (metadata.TryGetValue("Creator, attributed", out outValue))
			{
				authorString = GetAuthor(key.ToString(), outValue, "", ref creators);
			}
			else if (metadata.TryGetValue("PhotoCredit", out outValue)
				&& !outValue.Contains(", Code: "))
			{
				authorString = GetAuthor(key.ToString(), outValue, "", ref creators);
			}
			else if (metadata.TryGetValue("Creator", out outValue)
				&& !outValue.Contains(", Code: "))
			{
				authorString = GetAuthor(key.ToString(), outValue, "", ref creators);
			}

			if (metadata.TryGetValue("Author", out outValue))
			{
				infoTemplate = "Information";
				authorString = GetAuthor(key.ToString(), outValue, "", ref creators);
			}
			if (metadata.TryGetValue("Camera Information", out string cameraInfo) && cameraInfo == "Better Light Better Light, Model Super8k")
			{
				infoTemplate = "Information";
			}

			// determine the creation date
			//TODO: take oldest
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
				System.DateTime timeSubmitted = System.DateTime.Parse(outValue);
				if (dateMetadata.IsPrecise && timeSubmitted.Year < dateMetadata.PreciseYear)
				{
					// The current create date is definitely wrong. Try embedded time.
					System.DateTime embeddedDateTime;
					if (embeddedTimestampMetadata != DateParseMetadata.Unknown
						&& System.DateTime.TryParse(metadata["Embedded Timestamp"], out embeddedDateTime)
						&& (embeddedDateTime.Month != 1 || embeddedDateTime.Day != 1))
					{
						dateMetadata = embeddedTimestampMetadata;
						date = embeddedTimestamp;
					}
					else
					{
						throw new Exception("Time Submitted is before than Create/Content Date(s)");
					}
				}
			}

			string licenseTag = GetLicenseTag(authorString, creators, dateMetadata.LatestYear, m_config.defaultPubCountry);

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
			bool needFopCheck = false;
			if (metadata.TryGetValue("AltText", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue, ref needFopCheck) || needScopeCheck;
			}
			if (metadata.TryGetValue("Image Details", out outValue))
			{
				if (outValue.Contains("MapId:"))
				{
					infoTemplate = "Information";
				}

				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue, ref needFopCheck) || needScopeCheck;
			}
			if (metadata.TryGetValue("Description", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue, ref needFopCheck) || needScopeCheck;
			}
			if (metadata.TryGetValue("Table Of Contents", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue, ref needFopCheck) || needScopeCheck;
			}
			if (metadata.TryGetValue("Comment", out outValue))
			{
				needScopeCheck = !AddDescriptionBlock(descriptionBlocks, outValue, ref needFopCheck) || needScopeCheck;
			}
			if (metadata.TryGetValue("Subject", out outValue))
			{
				descriptionBlocks.AddUnique("*Subject: " + outValue);
			}
			string description;
			if (descriptionBlocks.Count == 0)
			{
				metadata.TryGetValue("Title", out description);
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
				throw new UploadDeclinedException("Out of scope?");
			}

			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new LicenseException(dateMetadata.LatestYear, m_config.defaultPubCountry);
			}

			bool useCopyrightStatement = false;
			if (metadata.TryGetValue("Constraints Information", out outValue))
			{
				if (outValue == "Restrictions apply on use and/or reproduction"
					|| outValue == "Restrictions apply on use and/or reproduction:This image is for research and study purposes only. This work may be protected by U.S. copyright law (Title 17, U.S. Code), which governs reproduction, distribution, public display, and other uses of protected works. Uses may be allowed with permission from the copyright holder, or if the copyright on the work has expired, or if the use is \"fair use\" or within another exemption. The user of this work is responsible for compliance with the law."
					|| outValue == "NO KNOWN COPYRIGHT: The organization that has made the Item available reasonably believes that the Item is not restricted by copyright or related rights, but a conclusive determination could not be made. Please refer to the organization that has made the Item available for more information. You are free to use this Item in any way that is permitted by the copyright and related rights legislation that applies to your use. Reference http://rightsstatements.org/vocab/NKC/1.0/"
					|| outValue == "Restrictions apply on use and/or reproduction:Copyright Unknown"
					|| outValue == "Restrictions apply on use and/or reproduction:Copyrighted material"
					|| outValue == "Restrictions apply on use and/or reproduction (Copyrighted material):Full Granting Rights"
					|| outValue == "Restrictions apply on use and/or reproduction:Full Granting Rights:Contact the park for use rights and restrictions."
					|| outValue == "Public domain:Abandoned mineral features may pose safety hazards, be archeological sites, or be endangered species habitat."
					|| outValue == "Restrictions apply on use and/or reproduction:Copyright Undetermined."
					|| outValue == "Restrictions apply on use and/or reproduction:Copyright Undetermined. For more information about this digital asset, contact Hawaii Volcanoes National Park, Museum Program, at havo_archive_museum@nps.gov"
					|| outValue == "Restrictions apply on use and/or reproduction:NO COPYRIGHT. For more information about this digital asset, contact Hawaii Volcanoes National Park, Museum Program, at havo_archive_museum@nps.gov"
					|| outValue == "Public domain"
					|| outValue == "Public domain:Harvard University"
					|| outValue == "Public domain:Full Granting Rights"
					|| outValue == "Public domain:Partial Granting Rights"
					|| outValue == "Public domain:Minimum Granting Rights"
					|| outValue == "Public domain:No known restrictions on publication."
					|| outValue == "Public domain:This photo was taken by staff at Big Bend National Park and is part of the public domain."
					|| outValue.StartsWith("Public domain:This digital asset is in the public domain."))
				{
					int creditIndex = outValue.IndexOf("When publishing this asset for any use, including online, image must credit:");
					if (creditIndex < 0)
					{
						creditIndex = outValue.IndexOf("When using this asset for any purpose, including online, credit:");
					}

					if (creditIndex >= 0)
					{
						int quoteOpenIndex = outValue.IndexOf("'", creditIndex);
						int quoteCloseIndex = outValue.IndexOf("'", quoteOpenIndex + 1);
						if (quoteOpenIndex < 0 || quoteCloseIndex < 0)
						{
							throw new Exception("'Constraints Information' contained something unrecognized");
						}
						metadata["Copyright"] = "The NPS requests that you attribute any use of this image with " + outValue.Substring(quoteOpenIndex, quoteCloseIndex - quoteOpenIndex + 1);
						useCopyrightStatement = true;
					}
					else
					{
						// straight PD
					}
				}
				else if (outValue == "Public domain:however please use copyright statement.")
				{
					useCopyrightStatement = true;
				}
				else if (outValue == "All Rights Reserved"
					|| outValue == "All Rights Reserved:Minimum Granting Rights")
				{
					throw new UploadDeclinedException("All Rights Reserved");
				}
				else if (!outValue.Contains(", Code: "))
				{
					throw new Exception("'Constraints Information' contained something unrecognized");
				}
			}
			else if (metadata.TryGetValue("Attribution", out outValue))
			{
				if (!outValue.Contains(", Code: ")
					&& outValue != "Restrictions apply on use and/or reproduction")
				{
					throw new Exception("'Attribution' contained something other than a park name");
				}
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
				foreach (PageTitle category in CategoryTranslation.TranslateLocationCategory(parkName))
				{
					string definiteLocation = category.Name;

					if (dateMetadata.IsPrecise)
					{
						PageTitle yearLocCat = new PageTitle("Category", dateMetadata.PreciseYear.ToString() + " in " + definiteLocation);
						Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
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
			string location = "";

			if (metadata.TryGetValue("Locations", out outValue))
			{
				string[] locationSplit = outValue.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries);
				if (locationSplit.Length % 2 == 1)
				{
					location = string.Join("; ", locationSplit);
				}
				else
				{
					for (int i = 0; i < locationSplit.Length; i++)
					{
						if (!locationSplit[i].StartsWith("Latitude: "))
						{
							location = StringUtility.Join("; ", location, locationSplit[i]);
						}
					}
				}
			}
			else
			{
				location = parkName;
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

					CollectCategories(categories, subject, parkCodes);
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
						CollectCategories(categories, dataTrimmed, parkCodes);
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
					CollectCategories(categories, subject, parkCodes);
				}
			}
			if (metadata.TryGetValue("Title", out outValue))
			{
				// first two words might be a genus/species
				int secondSpace = outValue.IndexOf(' ', outValue.IndexOf(' ') + 1);
				if (secondSpace >= 12)
				{
					CollectCategories(categories, outValue.Substring(0, secondSpace), parkCodes);
				}
			}	

			if (creators != null)
			{
				foreach (MappingCreator creator in creators)
				{
					throw new NotImplementedException();
					//if (!creator.Category.IsEmpty)
					//{
					//	categories.Add(creator.Category);
					//}
				}
			}

			string otherFields = "";
			if (metadata.TryGetValue("Collector", out outValue))
			{
				otherFields += "\n{{Information field|name=Collector|value={{en|1=" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Editor", out outValue))
			{
				otherFields += "\n{{Information field|name=Editor|value={{en|1=" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Sponsor", out outValue))
			{
				otherFields += "\n{{Information field|name=Sponsor|value={{en|1=" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Cataloguer", out outValue))
			{
				otherFields += "\n{{Information field|name=Cataloguer|value={{en|1=" + outValue + "}}}}";
			}
			if (metadata.TryGetValue("Contacts", out outValue))
			{
				otherFields += "\n{{Information field|name=Contacts|value={{en|1=" + outValue + "}}}}";
			}
			if (parkCodes.Count > 0)
			{
				otherFields += "\n{{Information field|name=NPS Unit Code|value=" + string.Join(", ", parkCodes) + "}}";
			}
			if (metadata.TryGetValue("Compiler", out outValue))
			{
				otherFields += "\n{{Information field|name=Compiler|value={{en|1=" + outValue + "}}}}";
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
						throw new UploadDeclinedException("Document");
					}
					otherFields += "\n{{Information field|name=" + systemKey  + "|value=" + systemValue + "}}";
				}
			}

			// display misc other keys
			foreach (KeyValuePair<string, string> kv in metadata)
			{
				if (!m_usedKeys.Contains(kv.Key))
				{
					otherFields += "\n{{Information field|name=" + kv.Key + "|value={{en|1=" + kv.Value + "}}}}";
				}
			}

			List<PageTitle> otherVersions = new List<PageTitle>();

			List<string> relatedAlbums = new List<string>();
			if (metadata.TryGetValue("~Related", out outValue) && !string.IsNullOrEmpty(outValue))
			{
				// album titles might work as categories
				string[] related = outValue.Split('|');
				for (int i = 0; i < related.Length; i += 3)
				{
					//TODO: do something with related assets?
					//TODO: handle unescaped pipes in entries
					string relatedId = related[i];
					string relatedType = related[i + 1];
					string relatedTitle = related[i + 2];
					if (relatedType == "Album")
					{
						relatedAlbums.Add(relatedTitle);

						CollectCategories(categories, relatedTitle, parkCodes);
					}
				}

				if (relatedAlbums.Count > 0)
				{
					otherFields += "\n{{Information field|name=Album(s)|value={{en|1=" + string.Join("; ", relatedAlbums) + "}}}}";
				}
				else if (related.Length == 3)
				{
					int i = 0;
					string relatedKeyStr = related[i];
					Guid relatedKey = StringToKey(relatedKeyStr);
					string relatedType = related[i + 1];
					string relatedTitle = related[i + 2];
					Dictionary<string, string> relatedMetadata = LoadMetadata(relatedKey, true);
					if (relatedMetadata == null)
					{
						relatedMetadata = m_downloader.Download(relatedKey, false);
					}
					PageTitle uploadTitle = GetTitle(relatedKey, relatedMetadata);
					uploadTitle.Name = uploadTitle.Name.Replace(s_badTitleCharacters, "");
					string imagePath = GetImageCacheFilename(key, metadata);
					uploadTitle.Name = uploadTitle.Name + Path.GetExtension(imagePath);
					otherVersions.Add(uploadTitle);
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
					if (new Regex("[0-9/]+ sec").Match(outValue).Success)
					{
						photoInfo += "|Shutter=" + outValue;
					}
					else if (outValue.Contains(" sec at "))
					{
						string[] split = outValue.Split(" sec at ", StringSplitOptions.None);
						if (split.Length != 2) throw new Exception("Could not parse 'Exposure'");
						photoInfo += "|Shutter=" + split[0];
						photoInfo += "|Aperture=" + split[1];
					}
					else
					{
						throw new Exception("Could not parse 'Exposure'");
					}
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
				authorString = "{{en|1=" + authorString + "}}";
			}

			if (parkCodes.Contains("JICA") && relatedAlbums.Any((album) => album.Contains("Postcard")))
			{
				throw new UploadDeclinedException("Jimmy Carter Postcard");
			}
			if (parkCodes.Contains("WEFA") && relatedAlbums.Contains("Youth art in the Parks COLT and WEFA"))
			{
				throw new UploadDeclinedException("Weir Farm Youth Art");
			}
			if (description.Contains("grave marker") && parkCodes.Contains("MACA"))
			{
				throw new UploadDeclinedException("Gravestone");
			}
			if (parkCodes.Contains("LONG"))
			{
				throw new UploadDeclinedException("LONG - letter?");
			}
			if (parkCodes.Contains("STEA"))
			{
				throw new UploadDeclinedException("STEA - documents?");
			}
			if (parkCodes.Contains("GETT"))
			{
				throw new UploadDeclinedException("GETT - letter?");
			}
			//HACK: skip children's art for now
			if (parkCodes.Contains("MNRR") || parkCodes.Contains("NERI"))
			{
				throw new UploadDeclinedException("Art?");
			}
			//HACK: skip Gettysburg on account of the volume of letters
			if (parkCodes.Contains("GETT"))
			{
				throw new UploadDeclinedException("Letter?");
			}

			// now that this looks like a success, redownload and start over
			if (!metadata.TryGetValue("~Version", out string version) || int.Parse(version) < NPGalleryDownloader.Version)
			{
				Console.WriteLine("Old version, redownloading");
				metadata = m_downloader.Download(key);
				if (metadata == null)
				{
					File.Delete(GetMetadataCacheFilename(key)); //HACK:?
					throw new Exception("Metadata redownload failed");
				}
				return BuildPage(key, metadata);
			}

			//TODO: captions

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			string catCheckTag = GetCheckCategoriesTag(categories.Count);
			categories.Add(PageTitle.Parse(m_config.checkCategory));
			if (needFopCheck)
			{
				categories.Add(new PageTitle(PageTitle.NS_Category, "Images from NPGallery to check for copyrighted sculptures"));
			}

			metadata.TryGetValue("Publisher", out string publisher);

			// if the date has an accurate month, day, and year, use {{taken on|}}
			if (infoTemplate == "Photograph"
				&& DateUtility.IsExactDateModern(date)
				// these are too frequently inaccurate
				&& !date.EndsWith("-01-01") && !date.EndsWith("-1-1"))
			{
				date = "{{taken on|" + date + "}}";
			}

			string title = "";
			metadata.TryGetValue("Title", out title);

			string page = catCheckTag + "\n"
				+ "=={{int:filedesc}}==\n"
				+ "{{" + infoTemplate + "\n";
			if (infoTemplate == "Photograph")
			{
				page += "|photographer=" + authorString + "\n";
				if (!string.IsNullOrEmpty(title))
				{
					page += "|title={{en|1=" + metadata["Title"] + "}}\n";
				}
				page += "|depicted place={{en|1=" + location + "}}\n"
					+ "|accession number={{NPGallery-accession|" + key + "}}\n";
				if (!string.IsNullOrEmpty(publisher))
				{
					page += "|publisher={{en|1=" + publisher + "}}\n";
				}
			}
			else
			{
				page += "|author=" + authorString + "\n";
				otherFields += "\n{{Information field|name=Depicted Place|value={{en|1=" + location + "}}}}"
					+ "\n{{Information field|name=Accession Number|value={{NPGallery-accession|" + key + "}}}}";
				if (!string.IsNullOrEmpty(publisher))
				{
					otherFields += "\n{{Information field|name=Publisher|value={{en|1=" + publisher + "}}}}";
				}
			}
			page += "|description=\n"
				+ "{{en|1=" + description + "}}\n"
				+ "|date=" + date + "\n"
				+ "|source=" + m_config.sourceTemplate + "\n";

			if (metadata.TryGetValue("Online Links to Resource", out outValue))
			{
				if (infoTemplate == "Photograph")
				{
					page += "|references=" + outValue + "\n";
				}
				else
				{
					otherFields += "\n{{Information field|name=References|value={{en|1=" + outValue + "}}}}";
				}
			}

			string otherVersionsString = "";
			if (otherVersions.Count > 0)
			{
				otherVersionsString = "<gallery>" + string.Join("\n", otherVersions.Select(t => t.Name).ToArray()) + "</gallery>";
			}

			page += "|permission=" + licenseTag + "\n";
			if (useCopyrightStatement)
			{
				string copyrightStatement;
				if (!metadata.TryGetValue("Copyright", out copyrightStatement))
				{
					throw new Exception("No requested Copyright statement");
				}
				else
				{
					page += "'''" + copyrightStatement + "'''\n";
				}
			}
			page += "|other_versions=" + otherVersionsString + "\n"
				+ "|other_fields=" + otherFields + "\n";
			if (!string.IsNullOrEmpty(photoInfo))
			{
				page += "{{Photo Information" + photoInfo + "}}\n";
			}
			page += "}}\n";

			foreach (PageTitle cat in categories)
			{
				page += "[[" + cat + "]]\n";
			}

			return page;
		}

		private void CollectCategories(HashSet<PageTitle> categories, string tag, IList<string> unitCodes)
		{
			IEnumerable<PageTitle> mappedCats = CategoryTranslation.TranslateTagCategory(tag);
			if (mappedCats != null)
			{
				foreach (PageTitle mappedCat in mappedCats)
				{
					// see if an "in (location)" or "of (location)" subcat exists and use that instead
					List<PageTitle> checkCats = new List<PageTitle>();
					foreach (string unitCode in unitCodes)
					{
						if (s_unitCodeToCommonsLoc.TryGetValue(unitCode, out string[] parentLocs))
						{
							foreach (string parentLoc in parentLocs)
							{
								checkCats.Add(new PageTitle(PageTitle.NS_Category, mappedCat + " in " + parentLoc));
								checkCats.Add(new PageTitle(PageTitle.NS_Category, mappedCat + " of " + parentLoc));
							}
						}
					}

					foreach (Article checkCatArt in CategoryTranslation.TryFetchCategories(Api, checkCats))
					{
						if (checkCatArt != null)
						{
							categories.Add(checkCatArt.title);
							return;
						}
					}

					categories.Add(mappedCat);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>False if the image needs a scope check</returns>
		private bool AddDescriptionBlock(List<string> descriptionBlocks, string descriptionBlock, ref bool checkFop)
		{
			descriptionBlock = descriptionBlock.RemoveIndentation().RemoveExtraneousLines();

			// do not add substrings of existing descriptions
			if (descriptionBlocks.Any(block => block.Contains(descriptionBlock)))
			{
				return true;
			}

			// remove substrings of new description
			descriptionBlocks.RemoveAll(block => descriptionBlock.Contains(block));

			descriptionBlocks.AddUnique(descriptionBlock);

			// check for FOP issues
			if (descriptionBlock.Contains(" statue "))
			{
				checkFop = true;
			}

			// add an additional check category to possible out-of-scope photos
			string[] split = descriptionBlock.ToLower().Split(' ');
			if (split.Length >= 2)
			{
				if (split[0] == "a" || split[0] == "one"
					|| split[0] == "two" || split[0] == "three"
					|| split[0] == "four" || split[0] == "five"
					|| split[0] == "six" || split[0] == "seven"
					|| split[0] == "eight" || split[0] == "nine"
					|| split[0] == "ten")
				{
					if (split[1] == "person" || split[1] == "people"
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

		public override void SaveOut()
		{
			base.SaveOut();

			NPSDirectoryQuery.SaveOut();
		}

		protected override bool TryAddDuplicate(PageTitle targetPage, Guid key, Dictionary<string, string> metadata)
		{
			Console.WriteLine("Checking to record duplicate " + key + " with existing page '" + targetPage + "'");

			// Fetch the target duplicate
			Article targetArticle = Api.GetPage(targetPage);
			string text = targetArticle.revisions[0].text;

			// Check that it's one of ours
			if (!text.Contains("|source=" + m_config.sourceTemplate))
			{
				return false;
			}

			// Check that the dupe link doesn't already exist
			int accessionIndex = text.IndexOf("{{NPGallery-accession|");
			while (accessionIndex >= 0)
			{
				int keyStart = accessionIndex + "{{NPGallery-accession|".Length;
				int keyEnd = text.IndexOf("}}", keyStart);
				string existingKey = text.Substring(keyStart, keyEnd - keyStart);
				Guid existingGuid = NPGallery.StringToKey(existingKey);
				if (existingGuid == key)
				{
					return false;
				}
				accessionIndex = text.IndexOf("{{NPGallery-accession|", keyEnd);
			}

			// Insert a link to the duplicate file
			int accessionPropIndex = text.IndexOf("|accession number=");
			if (accessionPropIndex < 0)
			{
				return false;
			}

			int insertionPoint = text.IndexOf('\n', accessionPropIndex);
			if (insertionPoint < 0)
			{
				return false;
			}

			string accession = "{{NPGallery-accession|" + key + "}}";
			text = text.Substring(0, insertionPoint) + "\n" + accession + text.Substring(insertionPoint);

			// submit the edit
			targetArticle.revisions[0].text = text;
			return Api.EditPage(targetArticle, "(BOT) recording duplicate record from NPGallery");
		}

		/// <summary>
		/// Returns true if the specified item should be marked as complete and not uploaded.
		/// </summary>
		public override bool ShouldSkipForever(Guid key, Dictionary<string, string> metadata)
		{
			using (MD5 md5 = MD5.Create())
			{
				string imagePath = GetImageCacheFilename(key, metadata);
				using (FileStream stream = File.OpenRead(imagePath))
				{
					byte[] hash = md5.ComputeHash(stream);
					if (hash.SequenceEqual(m_corruptedRecordErrorHash))
					{
						return true;
					}
					foreach (byte[] targetHash in m_notAuthorizedHashes)
					{
						if (hash.SequenceEqual(targetHash))
						{
							return true;
						}
					}
				}
			}

			return base.ShouldSkipForever(key, metadata);
		}

		public override void ValidateDownload(string imagePath)
		{
			base.ValidateDownload(imagePath);

			using (MD5 md5 = MD5.Create())
			{
				using (FileStream stream = File.OpenRead(imagePath))
				{
					byte[] hash = md5.ComputeHash(stream);
					if (hash.SequenceEqual(m_fileSystemErrorHash))
					{
						throw new Exception("File System Error");
					}
					if (hash.SequenceEqual(m_databaseErrorHash))
					{
						throw new Exception("File Database Error");
					}
				}
			}
		}

		protected override string GetAuthor(string fileKey, string name, string lang, ref List<MappingCreator> creator)
		{
			string organization = "";
			int italicStartIndex = name.IndexOf("<i>");
			if (italicStartIndex >= 0)
			{
				int italicEndIndex = name.IndexOf("</i>", italicStartIndex);
				organization = name.SubstringRange(italicStartIndex + "<i>".Length, italicEndIndex - 1);
				name = name.Substring(0, italicStartIndex - 1).TrimEnd();
			}

			string niceAuthor = base.GetAuthor(fileKey, name, lang, ref creator);
			if (!string.IsNullOrEmpty(organization))
			{
				niceAuthor += " (''" + organization + "'')";
			}
			return niceAuthor;
		}

		public override Uri GetImageUri(Guid key, Dictionary<string, string> metadata)
		{
			string url = string.Format(ImageUriFormat, key) + "original" + GetImageExtension(key, metadata);
			return new Uri(url);
		}

		public override string GetImageCacheFilename(Guid key, Dictionary<string, string> metadata)
		{
			return Path.ChangeExtension(base.GetImageCacheFilename(key, metadata), GetImageExtension(key, metadata));
		}

		public override string GetImageCroppedFilename(Guid key, Dictionary<string, string> metadata)
		{
			return Path.ChangeExtension(base.GetImageCroppedFilename(key, metadata), GetImageExtension(key, metadata));
		}

		private string GetImageExtension(Guid key, Dictionary<string, string> metadata)
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

		public override PageTitle GetTitle(Guid key, Dictionary<string, string> metadata)
		{
			string title = "", outValue;
			if (metadata.TryGetValue("Title", out outValue))
			{
				title = outValue;
			}
			if (string.IsNullOrEmpty(title) || title.Split(' ').Length <= 3)
			{
				if (metadata.TryGetValue("Description", out outValue))
				{
					if (title.Split(' ').Length < outValue.Split(' ').Length)
						title = outValue;
				}
			}
			if (string.IsNullOrEmpty(title))
			{
				throw new Exception("No title");
			}

			if (title.StartsWith("IMG_"))
			{
				if (metadata.TryGetValue("~Related", out string related) && related.Contains("74th National Pearl Harbor Remembrance Day"))
				{
					title = "74th National Pearl Harbor Remembrance Day";
				}
				else if (metadata.TryGetValue("AltText", out string alt))
				{
					title = alt;
				}
				else if (metadata.TryGetValue("Locations", out string locations))
				{
					title = locations;
				}
			}

			title = StringUtility.CleanHtml(title);
			title = HttpUtility.HtmlDecode(title);
			title = title.Replace("''", "\"");
			title = title.TrimStart("File-").TrimStart("File:");

			if (title.Length > 129)
			{
				//truncate the title to 128 characters on a word boundary
				int lastWordEnd = 0;
				for (int c = 0; c < 128; c++)
				{
					if (!char.IsLetterOrDigit(title[c + 1]) && char.IsLetterOrDigit(title[c]))
						lastWordEnd = c;
				}
				title = title.Remove(lastWordEnd + 1);
			}

			// add unique ID
			title += " (" + key + ")";

			return new PageTitle(PageTitle.NS_File, title);
		}
	}
}
