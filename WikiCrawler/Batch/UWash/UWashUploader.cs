﻿using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WikiCrawler;

namespace UWash
{
	public class UWashUploader : BatchUploader<int>
	{
		private string ImageUrlFormat
		{
			get
			{
				return "http://digitalcollections.lib.washington.edu/utils/ajaxhelper/?CISOROOT="
					+ UWashConfig.digitalCollectionsKey
					+ "&action=2&CISOPTR={0}&DMSCALE=100&DMWIDTH=99999&DMHEIGHT=99999&DMX=0&DMY=0&DMTEXT=&REC=1&DMTHUMB=0&DMROTATE=0";
			}
		}

		private static HashSet<string> s_knownKeys = new HashSet<string>()
		{
			//used
			"Title",
			"Alternative Title",
			"Subtitle",
			"Advertisement",
			"Photographer",
			"Cartographer",
			"Engraver",
			"Explorer",
			"Creator",
			"Artist",
			"Author",
			"Illustrator",
			"Original Creator",
			"Date of Photo",
			"Date",
			"Century Published",
			"Dates",
			"Publication Date",
			"Notes",
			"Caption Text",
			"Caption",
			"Descriptive Notes",
			"Contextual Notes",
			"Historical Notes",
			"Scrapbook Notes",
			"Album/Page",
			"Keywords",
			"Personal Names",
			"Subjects",
			"Subjects (LCTGM)",
			"Subjects (LCSH)",
			"Subjects (TGM)",
			"Category",
			"Location Depicted",
			"Location depicted",
			"Places",
			"Secondary Glacier",
			"Additional Glaciers",
			"Geographic Location",
			"Judicial District",
			"Mountain Range",
			"Preserve or Park",
			"Alternate Names",
			"Latitude",
			"Longitude",
			"Photographer's Altitude",
			"Order Number",
			"Physical Description",
			"Image Production Process",
			"Advertisement Text",
			"Company/Advertising Agency",
			"Publisher",
			"Printer",
			"Studio Name/Printer",
			"Publication Source",
			"Original Source",
			"Publisher Location",
			"Place of Publication",
			"Place of origin",
			"Publishing Notes",
			"Geographic Coverage",
			"Historical period",
			"Digital ID Number",
			"UW Reference Number",
			"Reference Number on Photograph",
			"Print Location",
			"Condition",
			"Credit Line",
			"Uniform Title",

			"LCSH",
			"LCTGM",
			"~art", //manual
			"LICENSE", //manual

			// unused
			"Object Description",
			"Object ID",
			"ID Number",
			"Brand Name/Product",
			"Digital Reproduction Information",
			"Rights URI",
			"Restrictions",
			"Object Type",
			"Type-DCMI",
			"Ordering Information",
			"Citation Information",
			"Repository",
			"Repository Collection",
			"Repository Collection Guide",
			"Digital Collection",
			"Collection Note",
			"Source",
			"Language",
			"Negative Number",
			"Negative number",
			"Negative",
			"Photographer's Reference Number",
			"Image Number",
			"Ordering and Contact Information",
			"Exhibit Checklist",
			"Acquisition",
			"References",
		};

		private UWashProjectConfig UWashConfig
		{
			get { return (UWashProjectConfig)m_config; }
		}

		/// <summary>
		/// Holds intermediate data for one media file that is passed around to various functions.
		/// </summary>
		private class IntermediateData
		{
			public Dictionary<string, string> Metadata;

			public HashSet<PageTitle> Categories = new HashSet<PageTitle>();

			public DateParseMetadata DateParseMetadata;

			public PageTitle PublisherLocCategory;

			public PageTitle SureLocationCategory;

			public string Creator;
			public string Artist;
			public string Contributor;

			public List<Creator> CreatorData = new List<Creator>();
			public List<Creator> ArtistData = new List<Creator>();
			public List<Creator> ContributorData = new List<Creator>();

			public int PublicDomainYear
			{
				get { return DateParseMetadata.LatestYear + 95; }
			}

			public IntermediateData(Dictionary<string, string> metadata)
			{
				Metadata = metadata;
			}
		}

		private static char[] s_punctuation = { '.', ' ', '\t', '\n', ',', '-' };
		private static string[] s_categorySplitters = { "|", ";", "<br/>", "<br />", "<br>" };
		private static string[] s_captionSplitters = new string[] { "--", "|" };

		public UWashUploader(string key)
			: base(key)
		{
			//HACK:
			m_config = JsonConvert.DeserializeObject<UWashProjectConfig>(
				File.ReadAllText(Path.Combine(ProjectDataDirectory, "config.json")));

			WebThrottle.Get(new Uri(ImageUrlFormat)).CrawlDelay = 15f;
		}

		public override Uri GetImageUri(int key, Dictionary<string, string> metadata)
		{
			return new Uri(string.Format(ImageUrlFormat, key));
		}

		/// <summary>
		/// Returns the title of the uploaded page for the specified metadata.
		/// </summary>
		public override PageTitle GetTitle(int key, Dictionary<string, string> metadata)
		{
			string title;
			if (!metadata.TryGetValue("Title", out title)
				&& !metadata.TryGetValue("Alternative Title", out title)
				&& !metadata.TryGetValue("Advertisement", out title))
			{
				throw new UWashException("No title");
			}

			title = title.Split(
				StringUtility.LineBreak,
				StringSplitOptions.RemoveEmptyEntries)[0].Trim(s_punctuation).Replace(".", "");
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

			if (!string.IsNullOrEmpty(m_config.filenameSuffix))
			{
				title = string.Format("{0} ({1} {2})", title, m_config.filenameSuffix, key);
			}

			return new PageTitle(PageTitle.NS_File, title);
		}

		/// <summary>
		/// Run any additional logic after a successful upload (such as uploading a crop).
		/// </summary>
		protected override void PostUpload(int key, Dictionary<string, string> metadata, Article article)
		{
			base.PostUpload(key, metadata, article);

			bool allowGeneralCrop = UWashConfig.allowCrop;
			bool allowWatermarkCrop = true;
			
			if (UWashConfig.filenameSuffix == "MOHAI"
				&& metadata.TryGetValue("Object Type", out string tempValue)
				&& tempValue.Contains("ephemera"))
			{
				allowGeneralCrop = false;
				allowWatermarkCrop = false;
			}

			// try to crop the image
			string imagePath = GetImageCacheFilename(key, metadata);
			string cropPath = GetImageCroppedFilename(key, metadata);
			bool watermarkCrop = false;
			bool otherCrop = false;
			if (allowWatermarkCrop)
			{
				watermarkCrop = ImageUtils.CropUwashWatermark(imagePath, cropPath);
			}
			if (allowGeneralCrop)
			{
				otherCrop = ImageUtils.AutoCropJpg(cropPath, cropPath, 0xffffffff, 0.95f, 16, new CropParams(ImageUtils.Side.Left | ImageUtils.Side.Right, true));
				System.Threading.Thread.Sleep(50);
				if (!File.Exists(cropPath))
				{
					throw new UWashException("crop failed");
				}
			}

			if (watermarkCrop || otherCrop)
			{
				// overwrite with the crop
				try
				{
					string crops = "";
					if (watermarkCrop)
					{
						crops = StringUtility.Join(", ", crops, "watermark");
					}
					if (otherCrop)
					{
						crops = StringUtility.Join(", ", crops, "horizontal");
					}

					string comment = "Automatic lossless crop (" + crops + ")";
					Article overwriteArticle = new Article(article.title);
					try
					{
						if (!Api.UploadFromLocal(overwriteArticle, cropPath, comment, true))
						{
							throw new UWashException("Crop upload failed");
						}
					}
					catch (DuplicateFileException e)
					{
						if (e.Duplicates.Length == 1 && e.IsSelfDuplicate)
						{
							// It looks like we already did this one, and that's fine
						}
						else if (m_config.succeedManualDupes)
						{

						}
						else
						{
							throw new UWashException(e.Message);
						}
					}
				}
				catch (WikimediaException e)
				{
					throw new UWashException(e.Message);
				}
			}
		}

		/// <summary>
		/// Builds the wiki page for the object with the specified metadata.
		/// </summary>
		protected override string BuildPage(int key, Dictionary<string, string> metadata)
		{
			metadata = PreprocessMetadata(metadata);
			IntermediateData intermediate = new IntermediateData(metadata);

			string lang;
			//if (metadata.TryGetValue("Language", out lang))
			//{
			//	lang = CommonsUtility.GetLanguageTemplate(lang);
			//}
			//else
			{
				lang = "en";
			}

			string temp;

			GetAuthor(metadata, lang, ref intermediate);
			
			string dateTag = GetDate(metadata, out intermediate.DateParseMetadata);

			int latestYear = intermediate.DateParseMetadata.LatestYear;

			// must have been published before author's death
			if (intermediate.DateParseMetadata.LatestYear == 9999 && intermediate.CreatorData.Count > 0)
			{
				int deathYearMax = intermediate.CreatorData.Max((Creator c) => c.DeathYear);
				if (latestYear == 9999)
				{
					latestYear = deathYearMax;
				}
				else
				{
					latestYear = Math.Max(latestYear, deathYearMax);
				}
			}
			if (intermediate.DateParseMetadata.LatestYear == 9999 && intermediate.ArtistData.Count > 0)
			{
				int deathYearMax = intermediate.ArtistData.Max((Creator c) => c.DeathYear);
				if (latestYear == 9999)
				{
					latestYear = deathYearMax;
				}
				else
				{
					latestYear = Math.Max(latestYear, deathYearMax);
				}
			}

			string pubCountry;
			if (metadata.TryGetValue("Place of Publication", out pubCountry))
			{
				pubCountry = LicenseUtility.ParseCountry(pubCountry);
			}
			else if (metadata.TryGetValue("Place of origin", out pubCountry))
			{
				pubCountry = LicenseUtility.ParseCountry(pubCountry);
			}
			else if (metadata.TryGetValue("Publisher Location", out pubCountry))
			{
				pubCountry = LicenseUtility.ParseCountry(pubCountry);
			}
			else
			{
				pubCountry = m_config.defaultPubCountry;
			}

			// check license
			string licenseTag;
			if (metadata.TryGetValue("LICENSE", out temp))
			{
				licenseTag = temp;
			}
			else
			{
				licenseTag = GetLicenseTag(intermediate.Creator, intermediate.CreatorData, latestYear, pubCountry);
			}
			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new LicenseException(intermediate.DateParseMetadata.LatestYear, pubCountry);
			}

			// check it's not a MOHAI modern photo
			string objectType;
			if (metadata.TryGetValue("Object Type", out objectType))
			{
				if (UWashConfig.filenameSuffix == "MOHAI" && objectType.Contains("artifact"))
				{
					throw new LicenseException(9999, "artifact");
				}
			}

			//check for new, unknown metadata
			string unused = "";
			foreach (KeyValuePair<string, string> kv in metadata)
			{
				string metaKey = kv.Key;
				if (!s_knownKeys.Contains(metaKey))
				{
					unused += "|" + kv.Key;
				}
			}
			if (!string.IsNullOrEmpty(unused)) throw new UWashException("unused key" + unused);

			// check for metadata with unexpected value
			string objectDescription;
			if (metadata.TryGetValue("Object Description", out objectDescription))
			{
				if (objectDescription != "Photograph")
				{
					throw new UWashException("unused key|Object Description");
				}
			}

			if (intermediate.DateParseMetadata.LatestYear < 1897 && intermediate.Creator == "{{Creator:Asahel Curtis}}")
			{
				//throw new UWashException("Curtis - check author");
			}

			// collect all the categories that should be used
			ProduceCategories(intermediate);

			//======== BUILD PAGE TEXT

			StringBuilder content = new StringBuilder();

			content.AppendLine(GetCheckCategoriesTag(intermediate.Categories.Count));
			if (!m_config.omitCheckWarning)
			{
				content.AppendLine("{{UWash-Check-Needed|" + m_config.collectionName + "}}");
			}

			string informationTemplate = m_config.informationTemplate;
			if (metadata.ContainsKey("~art"))
			{
				informationTemplate = "Artwork";
				licenseTag = "{{PD-Art|" + licenseTag.Trim('{', '}') + "}}";
			}

			content.AppendLine("=={{int:filedesc}}==");
			content.AppendLine("{{" + informationTemplate);
			if (informationTemplate == "Photograph")
			{
				content.AppendLine("|photographer=" + intermediate.Creator + intermediate.Contributor);
			}
			else if (informationTemplate == "Artwork")
			{
				if (m_projectKey == "childrens") //HACK:
				{
					content.AppendLine("|author=" + intermediate.Creator + intermediate.Contributor);
				}
				else
				{
					content.AppendLine("|artist=" + intermediate.Creator + intermediate.Contributor);
				}
			}
			else if (informationTemplate == "Map")
			{
				content.AppendLine("|author=" + intermediate.Creator);
				content.AppendLine("|contributor=" + intermediate.Contributor);
			}
			else
			{
				content.AppendLine("|author=" + intermediate.Creator + intermediate.Contributor);
			}

			if (!string.IsNullOrEmpty(intermediate.Artist))
			{
				content.AppendLine("|artist=" + intermediate.Artist);
			}

			if (informationTemplate != "Information")
			{
				string title = metadata["Title"];
				if (metadata.TryGetValue("Subtitle", out temp))
				{
					title += " — " + temp;
				}
				content.AppendLine("|title={{" + lang + "|1=" + title + "}}");
			}
			content.AppendLine("|description=");
			StringBuilder descText = new StringBuilder();

			List<string> notes = new List<string>();
			if (metadata.TryGetValue("Caption", out temp))
			{
				notes.Add(temp);
			}
			if (metadata.TryGetValue("Notes", out temp))
			{
				notes.AddRange(temp.Split(new string[] { @"\n\n" }, StringSplitOptions.RemoveEmptyEntries));
			}
			if (metadata.TryGetValue("Descriptive Notes", out temp))
			{
				notes.Add(temp);
			}
			if (metadata.TryGetValue("Contextual Notes", out temp))
			{
				notes.Add(temp);
			}
			if (metadata.TryGetValue("Historical Notes", out temp))
			{
				notes.Add(temp);
			}
			if (metadata.TryGetValue("Scrapbook Notes", out temp))
			{
				notes.Add(temp);
			}
			if (notes.Count == 0)
			{
				notes.Add(metadata["Title"]);
			}

			bool multipleDescs = notes.Count > 1;
			if (multipleDescs)
			{
				descText.AppendLine("<p>" + string.Join("</p>\n<p>", notes) + "</p>");
			}
			else
			{
				descText.AppendLine(notes[0]);
			}

			if (metadata.TryGetValue("Geographic Coverage", out temp))
			{
				descText.AppendLine("*Geographic coverage: " + temp.Trim());
			}
			if (metadata.TryGetValue("Subjects", out temp))
			{
				descText.AppendLine("*Subjects: " + temp.Trim());
			}
			if (metadata.TryGetValue("LCTGM", out temp))
			{
				descText.AppendLine("*Subjects (LCTGM): " + temp.Replace("|", "; "));
			}
			if (metadata.TryGetValue("LCSH", out temp))
			{
				descText.AppendLine("*Subjects (LCSH): " + temp.Replace("|", "; "));
			}
			if (metadata.TryGetValue("Category", out temp))
			{
				descText.AppendLine("*Categories: " + temp.Replace(StringUtility.LineBreak, "; "));
			}
			if (metadata.TryGetValue("Keywords", out temp))
			{
				descText.AppendLine("*Keywords: " + temp.Replace(StringUtility.LineBreak, "; "));
			}
			if (metadata.TryGetValue("Personal Names", out temp))
			{
				descText.AppendLine("*People: " + temp.Replace(StringUtility.LineBreak, "; "));
			}

			if (descText.Length > 0)
			{
				content.Append("{{en|1=");
				if (multipleDescs)
				{
					content.AppendLine();
				}
				content.Append(descText.ToString().Trim());
				content.AppendLine("}}");
			}

			if (!intermediate.SureLocationCategory.IsEmpty)
			{
				content.AppendLine("|depicted place=" + intermediate.SureLocationCategory.Name);
			}
			else if (metadata.TryGetValue("Location Depicted", out temp))
			{
				content.AppendLine("|depicted place={{" + lang + "|1=" + temp + "}}");
			}
			else if (metadata.TryGetValue("Location depicted", out temp))
			{
				content.AppendLine("|depicted place={{" + lang + "|1=" + temp + "}}");
			}
			else if (metadata.TryGetValue("Places", out temp))
			{
				content.AppendLine("|depicted place={{" + lang + "|1=" + temp + "}}");
			}

			string placeOfCreation;
			if (metadata.TryGetValue("Place of Publication", out placeOfCreation))
			{
				IEnumerable<PageTitle> locCats = CategoryTranslation.TranslateLocationCategory(placeOfCreation);
				PageTitle placeOfCreationCat = locCats == null ? PageTitle.Empty : locCats.FirstOrDefault();
				string placeOfCreationContent;
				if (!placeOfCreationCat.IsEmpty)
				{
					placeOfCreationContent = placeOfCreationCat.Name;
				}
				else
				{
					placeOfCreationContent = "{{" + lang + "|1=" + placeOfCreation + "}}";
				}
				if (informationTemplate == "Map")
				{
					content.AppendLine("|publication place=" + placeOfCreationContent);
				}
				else
				{
					content.AppendLine("|place of creation=" + placeOfCreationContent);
				}
			}

			string caption;
			if (metadata.TryGetValue("Caption Text", out caption))
			{
				caption = caption.Replace("|", "<br/>\n");
				content.AppendLine("|inscriptions={{inscription|language=en|" + caption + "}}");
			}

			content.AppendLine("|date=" + dateTag);
			if (metadata.TryGetValue("Physical Description", out temp))
			{
				if (metadata.ContainsKey("Image Production Process"))
				{
					throw new UWashException("Both 'Physical Description' and 'Image Production Process'");
				}

				string medium;
				Dimensions dimensions;
				ParsePhysicalDescription(temp, out medium, out dimensions);
				medium = medium.Trim();
				if (!string.IsNullOrEmpty(medium))
				{
					content.AppendLine("|medium={{en|1=" + medium + "}}");
				}
				if (!dimensions.IsEmpty)
				{
					// check the dimensions against the image
					//TODO:

					content.AppendLine("|dimensions=" + dimensions.GetCommonsTag());
				}
			}
			else if (metadata.TryGetValue("Image Production Process", out temp))
			{
				string[] techniques = temp.Split(';');
				string noun;
				string templateParams = string.Empty;
				foreach (string technique in techniques)
				{
					if (TryMapTechnique(technique, out noun))
					{
						if (string.IsNullOrEmpty(templateParams))
						{
							templateParams = noun;
						}
						else
						{
							throw new UWashException("Multiple techniques");
						}
					}
					else
					{
						throw new UWashException("Unmapped technique '" + technique + "'");
					}
				}
				if (!string.IsNullOrEmpty(templateParams))
				{
					content.AppendLine("|medium={{technique|" + templateParams + "}}");
				}
			}

			string otherFields = "";

			if (informationTemplate == "Information")
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Institution|value={{Institution:University of Washington}}}}");
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Department|value=" + UWashConfig.department + "}}");
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Accession number|value={{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}}}");
			}
			else if (informationTemplate == "Map")
			{
				content.AppendLine("|institution={{Institution:University of Washington}}\n" + UWashConfig.department);
				content.AppendLine("|accession number={{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}");
			}
			else
			{
				content.AppendLine("|institution={{Institution:University of Washington}}");
				content.AppendLine("|department=" + UWashConfig.department);
				content.AppendLine("|accession number={{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}");
			}

			content.AppendLine("|source=" + UWashConfig.sourceTemplate); //was department, for Artwork
			content.AppendLine("|permission=" + licenseTag);

			if (metadata.TryGetValue("Print Location", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Print Location|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Advertisement Text", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Inscriptions|value={{Inscription|" + temp + "|type=text|language=en}}}}");
			}
			if (metadata.TryGetValue("Publishing Notes", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publishing Notes| value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Publisher", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Printer", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Printer|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Studio Name/Printer", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Printer|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Order Number", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Order Number|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Album/Page", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Album/Page|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Digital ID Number", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Digital ID Number|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("UW Reference Number", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=UW Reference Number|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Reference Number on Photograph", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Reference Number on Photograph|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Company/Advertising Agency", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Company/Advertising Agency|value=" + temp + "}}");
			}
			if (!intermediate.PublisherLocCategory.IsEmpty)
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher Location|value=" + intermediate.PublisherLocCategory.Name + "}}");
			}
			if (metadata.TryGetValue("Publication Source", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publication Source|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Original Source", out temp))
			{
				if (informationTemplate == "Map")
				{
					content.AppendLine("|book_title=" + temp); //HACK: manually separate data after
				}
				else
				{
					otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Original Source|value=" + temp + "}}");
				}
			}
			if (metadata.TryGetValue("Historical period", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Historical Period|value=" + temp.Trim(';') + "}}");
			}
			if (metadata.TryGetValue("Judicial District", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Judicial District|value=" + temp.Trim(';') + "}}");
			}
			if (metadata.TryGetValue("Mountain Range", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Mountain Range|value=" + temp.Trim(';') + "}}");
			}
			if (metadata.TryGetValue("Preserve or Park", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Preserve or Park|value=" + temp.Trim(';') + "}}");
			}
			if (metadata.TryGetValue("Photographer's Altitude", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Camera Altitude|value=" + temp.Trim(';') + "}}");
			}
			if (metadata.TryGetValue("Alternate Names", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Alternate Name(s)|value=" + temp.Trim(';') + "}}");
			}
			if (metadata.TryGetValue("Condition", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Condition|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Uniform Title", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Uniform Title|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("Credit Line", out temp))
			{
				if (temp.EndsWith("[image number") || temp.EndsWith("[ID number"))
				{
					string imageNumber;
					if (!metadata.TryGetValue("Image Number", out imageNumber)
						&& !metadata.TryGetValue("ID Number", out imageNumber))
					{
						throw new UWashException("No Image Number for wildcard");
					}

					imageNumber = imageNumber.Trim();

					temp = temp.Replace("[image number", imageNumber);
					temp = temp.Replace("[ID number", imageNumber);
				}
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Credit Line|value=" + temp + "}}");
			}

			string additionalGlaciers = "";
			if (metadata.TryGetValue("Secondary Glacier", out temp))
			{
				additionalGlaciers = StringUtility.Join("; ", additionalGlaciers, temp);
			}
			if (metadata.TryGetValue("Additional Glaciers", out temp))
			{
				additionalGlaciers = StringUtility.Join("; ", additionalGlaciers, temp);
			}
			if (!string.IsNullOrEmpty(additionalGlaciers))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Additional Glaciers| value=" + additionalGlaciers.Trim(';') + "}}");
			}

			content.AppendLine("|other_versions=");

			if (!string.IsNullOrEmpty(otherFields))
			{
				content.AppendLine("|other_fields=\n" + otherFields);
			}

			content.AppendLine("}}");

			string lat, lon;
			if (metadata.TryGetValue("Latitude", out lat) && metadata.TryGetValue("Longitude", out lon))
			{
				content.AppendLine(GetObjectLocation(lat, lon));
			}

			content.AppendLine();
			
			if (m_config.omitCheckWarning && !string.IsNullOrEmpty(m_config.checkCategory))
			{
				content.AppendLine("[[" + WikiUtils.GetCategoryCategory(m_config.checkCategory) + "]]");
			}
			if (m_config.additionalCategories != null)
			{
				foreach (string category in m_config.additionalCategories)
				{
					content.AppendLine("[[" + category + "]]");
				}
			}
			foreach (Creator creator in intermediate.CreatorData)
			{
				if (!creator.Category.IsEmpty)
				{
					content.AppendLine("[[" + creator.Category + "]]");
				}
			}
			foreach (Creator creator in intermediate.ArtistData)
			{
				if (!creator.Category.IsEmpty)
				{
					content.AppendLine("[[" + creator.Category + "]]");
				}
			}

			foreach (PageTitle t in intermediate.Categories)
			{
				content.AppendLine("[[" + t + "]]");
			}

			if (latestYear == 9999)
			{
				throw new UWashException("unknown year");
			}

			foreach (Creator creator in intermediate.CreatorData)
			{
				creator.UploadableUsage++;
				if (string.IsNullOrEmpty(creator.Author))
				{
					if (!m_config.allowFailedCreators)
					{
						throw new UWashException("unrecognized creator '" + intermediate.Creator + "'");
					}
				}
			}
			if (intermediate.CreatorData.Count == 0 && !m_config.allowFailedCreators)
			{
				throw new UWashException("unrecognized creator '" + intermediate.Creator + "'");
			}

			foreach (Creator creator in intermediate.ArtistData)
			{
				creator.UploadableUsage++;
				if (string.IsNullOrEmpty(creator.Author))
				{
					if (!m_config.allowFailedCreators)
					{
						throw new UWashException("unrecognized creator '" + intermediate.Creator + "'");
					}
				}
			}
			if (intermediate.CreatorData.Count == 0 && !m_config.allowFailedCreators)
			{
				throw new UWashException("unrecognized creator '" + intermediate.Creator + "'");
			}
			if (!string.IsNullOrEmpty(intermediate.Artist) && intermediate.ArtistData.Count == 0 && !m_config.allowFailedCreators)
			{
				throw new UWashException("unrecognized creator '" + intermediate.Artist + "'");
			}

			return content.ToString();
		}

		/// <summary>
		/// Determines which categories the image should go in.
		/// </summary>
		private void ProduceCategories(IntermediateData data)
		{
			Dictionary<string, string> metadata = data.Metadata;
			HashSet<PageTitle> categories = data.Categories;
			string outValue;

			// pipe-delimited categories to parse
			string catparse = "";

			//categories for people
			/*if (data.ContainsKey("Artist/Photographer"))
			{
				foreach (string auth in ParseAuthor(data["Artist/Photographer"]))
					catparse += "|" + auth;
			}
			else if (data.ContainsKey("Image Source Author"))
			{
				foreach (string auth in ParseAuthor(data["Image Source Author"]))
					catparse += "|" + auth;
			}
			foreach (string s in catparse.Split(pipe, StringSplitOptions.RemoveEmptyEntries))
			{
				string cat = TranslatePersonCategory(s);
				if (!string.IsNullOrEmpty(cat))
				{
					if (cat != "N/A")
					{
						foreach (string catsplit in cat.Split('|'))
						{
							if (!categories.Contains(catsplit)) categories.Add(catsplit);
						}
					}
				}
				else
				{
					throw new UWashException("unknown cat");
				}
			}*/

			if (metadata.TryGetValue("Title", out outValue))
			{
				foreach (string split in outValue.Split(new string[] { " and ", "," }, StringSplitOptions.RemoveEmptyEntries))
				{
					//HACK: years are a bit too vague
					if (int.TryParse(split, out int result))
					{
						continue;
					}

					catparse += "|" + split.Trim();
				}
			}

			//categories for tags
			if (metadata.TryGetValue("LCTGM", out outValue)) catparse = CollectCategories(catparse, outValue);
			if (metadata.TryGetValue("LCSH", out outValue)) catparse = CollectCategories(catparse, outValue);
			if (metadata.TryGetValue("Category", out outValue)) catparse = CollectCategories(catparse, outValue);
			if (metadata.TryGetValue("Keywords", out outValue)) catparse = CollectCategories(catparse, outValue);
			/*if (data.ContainsKey("Caption"))
			{
				//max 50
				foreach (string s in data["Caption"].Split(s_captionSplitters, StringSplitOptions.RemoveEmptyEntries))
				{
					if (s.Length < 50) catparse += "|" + s;
				}
			}*/
			foreach (string s in catparse.Split(s_categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				IEnumerable<PageTitle> cats = CategoryTranslation.TranslateTagCategory(s.Trim());
				if (cats != null)
				{
					foreach (PageTitle catsplit in cats)
					{
						if (!categories.Contains(catsplit)) categories.Add(catsplit);
					}
				}
			}

			//categories for names
			if (metadata.TryGetValue("Personal Names", out outValue))
			{
				foreach (string rawName in outValue.Split('\n', ';'))
				{
					if (string.IsNullOrWhiteSpace(rawName)) continue;

					string[] nameSplit = rawName.Split(',');
					if (nameSplit.Length >= 2 && nameSplit.Length <= 3)
					{
						string nameAssembled = nameSplit[1].Trim() + " " + nameSplit[0].Trim();
						foreach (PageTitle cat in CategoryTranslation.TranslatePersonCategory(nameAssembled, false))
						{
							if (!categories.Contains(cat)) categories.Add(cat);
						}
					}
					else
					{
						foreach (PageTitle cat in CategoryTranslation.TranslateTagCategory(rawName))
						{
							if (!categories.Contains(cat)) categories.Add(cat);
						}
					}
				}
			}

			//categories for locations
			catparse = "";
			if (metadata.TryGetValue("Location Depicted", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Location depicted", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Geographic Location", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Mountain Range", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Preserve or Park", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Judicial District", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Secondary Glacier", out outValue)) catparse += "|" + outValue;
			if (metadata.TryGetValue("Additional Glaciers", out outValue)) catparse += "|" + outValue;
			foreach (string s in catparse.Split(s_categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				IEnumerable<PageTitle> cats = CategoryTranslation.TranslateLocationCategory(s.Trim());
				if (cats != null)
				{
					foreach (PageTitle catsplit in cats)
					{
						if (data.SureLocationCategory.IsEmpty) data.SureLocationCategory = catsplit;
						if (!categories.Contains(catsplit)) categories.Add(catsplit);
					}
				}
			}
			
			if (metadata.TryGetValue("Publisher Location", out string publisherLocation))
			{
				IEnumerable<PageTitle> pubLocCats = CategoryTranslation.TranslateLocationCategory(publisherLocation);
				data.PublisherLocCategory = pubLocCats == null ? PageTitle.Empty : pubLocCats.FirstOrDefault();
			}

			// advertisement categories
			if (metadata.ContainsKey("Advertisement") && data.DateParseMetadata.LatestYear != 9999)
			{
				string latestYearString = data.DateParseMetadata.LatestYear.ToString();
				PageTitle adYearCat = new PageTitle(PageTitle.NS_Category, latestYearString + " advertisements in the United States");
				Article existingYearCat = CategoryTranslation.TryFetchCategory(Api, adYearCat);
				if (existingYearCat == null)
				{
					existingYearCat = new Article(adYearCat);
					existingYearCat.revisions = new Revision[1];
					existingYearCat.revisions[0] = new Revision();
					existingYearCat.revisions[0].text = "{{AdvertisUSYear|" + latestYearString.Substring(0, 3)
						+ "|" + latestYearString.Substring(3) + "}}";
					Api.CreatePage(existingYearCat, "(BOT) creating category");
				}
				categories.Add(adYearCat);

				if (!data.PublisherLocCategory.IsEmpty)
				{
					PageTitle adLocCat = new PageTitle(PageTitle.NS_Category, "Advertisements in " + data.PublisherLocCategory.Name.Split(',')[0]);
					Article existingLocCat = CategoryTranslation.TryFetchCategory(Api, adLocCat);
					if (existingLocCat != null)
					{
						categories.Add(existingLocCat.title);
					}
					else
					{
						//throw new UWashException("No adloc cat: " + adLocCat);
					}
				}
			}

			// portrait categories
			if (metadata.TryGetValue("Digital Collection", out outValue))
			{
				if (outValue == "Portraits Collection")
				{
					if (metadata.TryGetValue("Object Type", out outValue))
					{
						if (outValue.Contains("Photo"))
						{
							int decade;
							if (data.DateParseMetadata.IsPrecise)
							{
								categories.Add(new PageTitle(PageTitle.NS_Category, string.Format("{0} portrait photographs", data.DateParseMetadata.PreciseYear)));
							}
							else if (DateUtility.GetDecade(data.DateParseMetadata, out decade))
							{
								categories.Add(new PageTitle(PageTitle.NS_Category, string.Format("{0}s portrait photographs", decade)));
							}

							// occupation
							foreach (PageTitle category in categories)
							{
								PageTitle portraitPhotosCat = new PageTitle(PageTitle.NS_Category, "Portrait photographs of " + category.Name.ToLower());
								Article existingCat = CategoryTranslation.TryFetchCategory(Api, portraitPhotosCat);
								if (!Article.IsNullOrMissing(existingCat))
								{
									categories.Add(existingCat.title);
									break;
								}
							}

							//HACK: assume US
							categories.Add(new PageTitle(PageTitle.NS_Category, "Portrait photographs of the United States"));
						}
					}
				}
			}

			// some other categories
			if (!data.SureLocationCategory.IsEmpty)
			{
				string sureLocation = data.SureLocationCategory.Name;

				if (PageNameComparer.Instance.Equals(m_config.informationTemplate, "Photograph"))
				{
					//TODO: check that the image is black and white
					if (false)
					{
						PageTitle blackAndWhiteCat = new PageTitle(PageTitle.NS_Category, "Black and white photographs of " + sureLocation);
						Article existingBwCat = CategoryTranslation.TryFetchCategory(Api, blackAndWhiteCat);
						if (existingBwCat != null)
						{
							categories.Add(existingBwCat.title);
						}
					}
				}

				if (data.DateParseMetadata.IsPrecise)
				{
					PageTitle yearLocCat = new PageTitle(PageTitle.NS_Category, data.DateParseMetadata.PreciseYear.ToString() + " in " + sureLocation);
					Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
					if (existingYearLocCat != null)
					{
						categories.Add(existingYearLocCat.title);
					}
				}
			}

			SayreCategorize.ProduceSayreCategories(Api, CategoryTranslation.CategoryTree, data.Metadata, data.Categories);

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			// Public Domain Day
			if (data.PublicDomainYear == System.DateTime.Now.Year && System.DateTime.Now.Month == 1)
			{
				string nameFormat = string.IsNullOrEmpty(m_config.publicDomainDayCategory)
					? "Media uploaded for Public Domain Day {0}"
					: m_config.publicDomainDayCategory;
				categories.Add(new PageTitle(PageTitle.NS_Category, string.Format(nameFormat, System.DateTime.Now.Year)));
			}
		}

		private string CollectCategories(string accumulator, string newCats)
		{
			if (newCats.Contains(';'))
			{
				// line breaks are probably not separators
				return accumulator + "|" + newCats.Replace(StringUtility.LineBreak, " ");
			}
			else
			{
				return accumulator + "|" + newCats.Replace(StringUtility.LineBreak, "|");
			}
		}

		protected override bool TryAddDuplicate(PageTitle targetPage, int key, Dictionary<string, string> metadata)
		{
			Console.WriteLine("Checking to record duplicate {0} with existing page '{1}'", key, targetPage);

			// Fetch the target duplicate
			Article targetArticle = Api.GetPage(targetPage);
			string text = targetArticle.revisions[0].text;

			// Check that it's one of ours
			if (!text.Contains("|source=" + m_config.sourceTemplate))
			{
				return false;
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

			string accession = "{{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}";
			if (text.Contains(accession))
			{
				return false;
			}
			text = text.Substring(0, insertionPoint) + " " + accession + text.Substring(insertionPoint);

			// submit the edit
			targetArticle.revisions[0].text = text;
			return Api.EditPage(targetArticle, "(BOT) recording duplicate record from UWash collection");
		}

		private static Dictionary<string, string> PreprocessMetadata(Dictionary<string, string> data)
		{
			string lctgm, lcsh;

			if (!data.TryGetValue("LCTGM", out lctgm))
			{
				lctgm = "";
			}
			if (!data.TryGetValue("LCSH", out lcsh))
			{
				lcsh = "";
			}

			string temp;
			if (data.TryGetValue("Subjects", out temp))
			{
				//HACK: is this always LCTGM? Added for imlsmohai
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects");
			}
			if (data.TryGetValue("Subjects (TGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects (TGM)");
			}
			if (data.TryGetValue("Subjects (LCTGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects (LCTGM)");
			}
			if (data.TryGetValue("Subjects(LCTGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects(LCTGM)");
			}
			if (data.TryGetValue("Subjects (LCTGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects (LCTGM)");
			}
			if (data.TryGetValue("Subjects (LCTGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects (LCTGM)");
			}
			if (data.TryGetValue("Subjects (LCSH)", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("Subjects (LCSH)");
			}
			if (data.TryGetValue("Subject (LCSH)", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("Subject (LCSH)");
			}
			if (data.TryGetValue("Subjects (LCSH)", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("Subjects (LCSH)");
			}

			if (!string.IsNullOrEmpty(lcsh))
			{
				data["LCSH"] = lcsh.Replace(StringUtility.LineBreak, "|");
			}
			if (!string.IsNullOrEmpty(lctgm))
			{
				data["LCTGM"] = lctgm.Replace(StringUtility.LineBreak, "|");
			}

			return data;
		}

		private static readonly Regex CoordinateRegex = new Regex("^\\s*(\\d+)° (\\d+)' (\\d+\\.?\\d*)\" ([NESW])\\s*$");

		private string GetObjectLocation(string lat, string lon)
		{
			Match latMatch = CoordinateRegex.Match(lat);
			Match lonMatch = CoordinateRegex.Match(lon);
			if (latMatch.Success && lonMatch.Success)
			{
				return string.Format("{{{{object location|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}}}}}",
					latMatch.Groups[1].Value,
					latMatch.Groups[2].Value,
					latMatch.Groups[3].Value,
					latMatch.Groups[4].Value,
					lonMatch.Groups[1].Value,
					lonMatch.Groups[2].Value,
					lonMatch.Groups[3].Value,
					lonMatch.Groups[4].Value);
			}
			else
			{
				throw new Exception("Failed to parse lat/lon");
			}
		}

		private bool GetSource(Dictionary<string, string> data, out string result)
		{
			if (data.ContainsKey("Image Source Title") || data.ContainsKey("Pub. Info."))
			{
				//Construct a cite-book template
				StringBuilder sb = new StringBuilder();
				sb.AppendLine();
				sb.AppendLine("{{Cite book");
				if (data.ContainsKey("Image Source Title"))
				{
					sb.AppendLine("|title=" + data["Image Source Title"]);
					if (data.ContainsKey("Image Source Series"))
					{
						sb.AppendLine("|series=" + data["Image Source Series"]);
					}
				}
				else if (data.ContainsKey("Image Source Series"))
				{
					sb.AppendLine("|title=" + data["Image Source Series"]);
				}
				if (data.ContainsKey("Image Source Author"))
				{
					string author = CleanPersonName(data["Image Source Author"]);
					string[] commasplit = author.Split(',');
					if (commasplit.Length == 2)
					{
						//interpret as last, first
						sb.AppendLine("|last=" + commasplit[0].Trim());
						sb.AppendLine("|first=" + commasplit[1].Trim());
					}
					else
					{
						sb.AppendLine("|author=" + author.Replace(StringUtility.LineBreak, "; "));
					}
				}
				if (data.ContainsKey("Pub. Info."))
				{
					string pubInfo = data["Pub. Info."];
					string[] split1 = pubInfo.Split(StringUtility.Colon, 2);
					if (split1.Length == 2)
					{
						int comma = split1[1].LastIndexOf(',');
						if (comma >= 0)
						{
							sb.AppendLine("|location=" + split1[0].Trim());
							sb.AppendLine("|publisher=" + split1[1].Substring(0, comma).Trim());
							sb.AppendLine("|year=" + split1[1].Substring(comma + 1).Trim());
						}
						else
						{
							sb.AppendLine("|publisher=" + pubInfo);
						}
					}
					else
					{
						sb.AppendLine("|publisher=" + pubInfo);
					}
				}
				if (data.ContainsKey("Page No./Plate No."))
				{
					string pagestring = data["Page No./Plate No."];
					if (pagestring.StartsWith("Page "))
					{
						sb.AppendLine("|page=" + pagestring.Substring(5));
					}
					else
					{
						sb.AppendLine("|at=" + pagestring);
					}
				}
				sb.Append("}}");
				result = sb.ToString();
				return true;
			}
			else if (data.ContainsKey("Image Source Series"))
			{
				result = data["Image Source Series"];
				return true;
			}
			else
			{
				result = "{{FMIB-source}}";
				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private void GetAuthor(Dictionary<string, string> data, string lang, ref IntermediateData intermediate)
		{
			string notes;
			if (data.TryGetValue("Notes", out notes)
				&& notes.Contains("Original photographer unknown"))
			{
				intermediate.Creator = "{{unknown|author}}";
				return;
			}

			string author;
			if (data.TryGetValue("Author", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);

				if (data.TryGetValue("Artist", out author)
					|| data.TryGetValue("Illustrator", out author))
				{
					intermediate.Artist += GetAuthor(author, lang, ref intermediate.ArtistData);
				}
			}
			if (data.TryGetValue("Original Creator", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Creator", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Artist", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Photographer", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Cartographer", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Engraver", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Company/Advertising Agency", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Publisher", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}
			if (data.TryGetValue("Illustrator", out author))
			{
				intermediate.Creator += GetAuthor(author, lang, ref intermediate.CreatorData);
			}

			if (string.IsNullOrEmpty(intermediate.Creator))
			{
				intermediate.Creator = GetAuthor(m_config.defaultAuthor, "en", ref intermediate.CreatorData);
			}

			if (data.TryGetValue("Explorer", out author))
			{
				intermediate.Contributor = GetAuthor(author, lang, ref intermediate.ContributorData);
			}
		}

		private string GetDate(Dictionary<string, string> data, out DateParseMetadata parseMetadata)
		{
			string date;
			if (!data.TryGetValue("Date of Photo", out date)
				&& !data.TryGetValue("Date", out date)
				&& !data.TryGetValue("Publication Date", out date)
				&& !data.TryGetValue("Dates", out date)
				&& !data.TryGetValue("Century Published", out date))
			{
				parseMetadata = DateParseMetadata.Unknown;
				return "{{unknown|date}}";
			}

			date = DateUtility.ParseDate(date, out parseMetadata);

			// if the date is just the year, there may be a more precise date at the end of the title
			if (date == parseMetadata.LatestYear.ToString())
			{
				string title;
				if (data.TryGetValue("Title", out title))
				{
					int commaIndex = title.IndexOf(',');
					while (commaIndex >= 0)
					{
						string possibleDate = title.Substring(commaIndex + 1).Trim();
						DateParseMetadata titleParseMetadata;
						try
						{
							string titleDate = DateUtility.ParseDate(possibleDate, out titleParseMetadata);
							if (titleParseMetadata.LatestYear == parseMetadata.LatestYear)
							{
								date = titleDate;
								break;
							}
						}
						catch (ArgumentException)
						{
							// if that didn't turn out to be a valid date, don't worry about it
						}
						commaIndex = title.IndexOf(',', commaIndex + 1);
					}
				}
			}

			// if the date has an accurate month, day, and year, use {{taken on|}}
			if (m_config.informationTemplate == "Photograph"
				&& DateUtility.IsExactDateModern(date))
			{
				date = "{{taken on|" + date + "}}";
			}

			return date;
		}

		private static string GetDesc(Dictionary<string, string> metadata)
		{
			string title;
			if (!metadata.TryGetValue("Title", out title))
			{
				throw new UWashException("no title");
			}

			List<string> raw = title.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries).ToList();
			string temp;
			if (metadata.TryGetValue("Notes", out temp))
			{
				raw.AddRange(temp.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries));
			}
			if (metadata.TryGetValue("Contextual Notes", out temp))
			{
				raw.AddRange(temp.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries));
			}

			Dictionary<string, string> textByLang = new Dictionary<string, string>();

			//treat first segment as base language
			textByLang[metadata["Language"]] = raw[0];

			//try to find language for other segments
			for (int c = 1; c < raw.Count; c++)
			{
				string raw2 = raw[c].Trim(StringUtility.Parens);
				string newlang = "en";//allowUpload ? DetectLanguage.Detect(raw2) : "en";
				if (textByLang.ContainsKey(newlang))
					textByLang[newlang] += "\n" + raw2;
				else
					textByLang[newlang] = raw2;
			}

			//metadata is always en
			if (metadata.ContainsKey("Subject"))
			{
				string content = "*Subject: " + metadata["Subject"].Replace(StringUtility.LineBreak, ", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}
			if (metadata.ContainsKey("Geographic Subject"))
			{
				string content = "*Geographic Subject: " + metadata["Geographic Subject"].Replace(StringUtility.LineBreak, ", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}
			if (metadata.ContainsKey("Category"))
			{
				string content = "*Tag: " + metadata["Category"].Replace(StringUtility.LineBreak, ", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}

			//build string
			string final = "";
			foreach (KeyValuePair<string, string> kv in textByLang)
			{
				if (textByLang.Count == 1 && !kv.Value.Contains("\n"))
				{
					return "{{" + kv.Key + "|" + kv.Value + "}}";
				}
				else
				{
					final += "\n{{" + kv.Key + "|" + kv.Value + "}}";
				}
			}
			return final;
		}

		public bool TryMapTechnique(string input, out string noun)
		{
			switch (input.ToUpper())
			{
				case "PLANOGRAPHIC PRINTS--LITHOGRAPHS":
					noun = "lithograph";
					return true;
				default:
					noun = string.Empty;
					return false;
			}
		}
	}
}
