﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using WikiCrawler;
using Wikimedia;

namespace UWash
{
	public class UWashUploader : BatchUploader
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
			"Advertisement",
			"Photographer",
			"Original Creator",
			"Date",
			"Publication Date",
			"Notes",
			"Contextual Notes",
			"Historical Notes",
			"Scrapbook Notes",
			"Album/Page",
			"Subjects (LCTGM)",
			"Subjects (LCSH)",
			"Subjects (TGM)",
			"Category",
			"Location Depicted",
			"Order Number",
			"Physical Description",
			"Advertisement Text",
			"Company/Advertising Agency",
			"Publisher",
			"Publication Source",
			"Publisher Location",
			"Geographic Coverage",
			"Digital ID Number",

			"LCSH",
			"LCTGM",

			// unused
			"Brand Name/Product",
			"Digital Reproduction Information",
			"Rights URI",
			"Restrictions",
			"Object Type",
			"Ordering Information",
			"Citation Information",
			"Repository",
			"Repository Collection",
			"Repository Collection Guide",
			"Digital Collection",
			"Source",
			"Negative Number",
			"Negative",
			"Photographer's Reference Number",
		};

		private UWashProjectConfig UWashConfig
		{
			get { return (UWashProjectConfig)m_config; }
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

			EasyWeb.SetDelayForDomain(new Uri(ImageUrlFormat), 15f);
			EasyWeb.SetDelayForDomain(new Uri("https://commons.wikimedia.org/"), 0.5f);
		}

		protected override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			return new Uri(string.Format(ImageUrlFormat, key));
		}

		/// <summary>
		/// Returns the title of the uploaded page for the specified metadata.
		/// </summary>
		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			string title;
			if (!metadata.TryGetValue("Title", out title)
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

			if (string.IsNullOrEmpty(m_config.filenameSuffix))
			{
				throw new UWashException("Missing required config filenameSuffix");
			}

			title = title + " (" + m_config.filenameSuffix + " " + key + ")";

			return title;
		}

		/// <summary>
		/// Run any additional logic after a successful upload (such as uploading a crop).
		/// </summary>
		protected override void PostUpload(string key, Article article)
		{
			base.PostUpload(key, article);

			// try to crop the image
			string imagePath = GetImageCacheFilename(key);
			string cropPath = GetImageCroppedFilename(key);
			bool watermarkCrop = ImageUtils.CropUwashWatermark(imagePath, cropPath);
			bool otherCrop = false;
			if (UWashConfig.allowCrop)
			{
				otherCrop = ImageUtils.AutoCropJpg(cropPath, cropPath, 0xffffffff, 0.95f, 16, ImageUtils.Side.Left | ImageUtils.Side.Right);
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
		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			metadata = PreprocessMetadata(metadata);

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

			string lang = "en";

			Creator creator;
			string author = GetAuthor(metadata, lang, out creator);

			List<string> categories = new List<string>();

			// pipe-delimited categories to parse
			string catparse = "";

			//categories for people
			/*catparse = "";
			if (data.ContainsKey("Artist/Photographer"))
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

			//categories for tags
			string temp;
			if (metadata.TryGetValue("LCTGM", out temp)) catparse += "|" + temp;
			if (metadata.TryGetValue("LCSH", out temp)) catparse += "|" + temp;
			if (metadata.TryGetValue("Category", out temp)) catparse += "|" + temp.Replace(StringUtility.LineBreak, "|");
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
				IEnumerable<string> cats = CategoryTranslation.TranslateTagCategory(s.Trim());
				if (cats != null)
				{
					foreach (string catsplit in cats)
					{
						if (!categories.Contains(catsplit)) categories.Add(catsplit);
					}
				}
			}

			//categories for locations
			string sureLocationCategory = "";
			catparse = "";
			if (metadata.TryGetValue("Location Depicted", out temp)) catparse += "|" + temp;
			foreach (string s in catparse.Split(s_categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				IEnumerable<string> cats = CategoryTranslation.TranslateLocationCategory(s.Trim());
				if (cats != null)
				{
					foreach (string catsplit in cats)
					{
						if (string.IsNullOrEmpty(sureLocationCategory)) sureLocationCategory = catsplit;
						if (!categories.Contains(catsplit)) categories.Add(catsplit);
					}
				}
			}

			DateParseMetadata dateParseMetadata;
			string dateTag = GetDate(metadata, out dateParseMetadata);

			string publisherLocation = "";
			if (metadata.TryGetValue("Publisher Location", out publisherLocation))
			{
				IEnumerable<string> pubLocCats = CategoryTranslation.TranslateLocationCategory(publisherLocation);
				publisherLocation = pubLocCats == null ? "" : pubLocCats.FirstOrDefault();
				if (publisherLocation.StartsWith("Category:")) publisherLocation = publisherLocation.Substring("Category:".Length);
			}

			// advertisement categories
			if (metadata.ContainsKey("Advertisement") && dateParseMetadata.LatestYear != 9999)
			{
				string latestYearString = dateParseMetadata.LatestYear.ToString();
				string adYearCat = "Category:" + latestYearString + " advertisements in the United States";
				Wikimedia.Article existingYearCat = CategoryTranslation.TryFetchCategory(Api, adYearCat);
				if (existingYearCat == null)
				{
					existingYearCat = new Wikimedia.Article(adYearCat);
					existingYearCat.revisions = new Wikimedia.Revision[1];
					existingYearCat.revisions[0] = new Wikimedia.Revision();
					existingYearCat.revisions[0].text = "{{AdvertisUSYear|" + latestYearString.Substring(0, 3)
						+ "|" + latestYearString.Substring(3) + "}}";
					Api.SetPage(existingYearCat, "(BOT) creating category", false, true, false);
				}
				categories.Add(adYearCat);

				if (!string.IsNullOrEmpty(publisherLocation))
				{
					string adLocCat = "Category:Advertisements in " + publisherLocation.Split(',')[0];
					Wikimedia.Article existingLocCat = CategoryTranslation.TryFetchCategory(Api, adLocCat);
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

			// some other categories
			if (!string.IsNullOrEmpty(sureLocationCategory))
			{
				string sureLocation = sureLocationCategory.Substring("Category:".Length);

				if (m_config.informationTemplate == "Photograph")
				{
					//TODO: check that the image is black and white
					if (false)
					{
						string blackAndWhiteCat = "Category:Black and white photographs of " + sureLocation;
						Wikimedia.Article existingBwCat = CategoryTranslation.TryFetchCategory(Api, blackAndWhiteCat);
						if (existingBwCat != null)
						{
							categories.Add(existingBwCat.title);
						}
					}
				}

				if (dateParseMetadata.PreciseYear != 0)
				{
					string yearLocCat = "Category:" + dateParseMetadata.PreciseYear.ToString() + " in " + sureLocation;
					Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
					if (existingYearLocCat != null)
					{
						categories.Add(existingYearLocCat.title);
					}
				}
			}

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			// must have been published before author's death
			int latestYear;
			if (creator != null && dateParseMetadata.LatestYear == 9999)
			{
				latestYear = creator.DeathYear;
			}
			else
			{
				latestYear = dateParseMetadata.LatestYear;
			}

			string pubCountry;
			if (metadata.TryGetValue("Place of Publication", out pubCountry))
			{
				pubCountry = LicenseUtility.ParseCountry(pubCountry);
			}
			else
			{
				pubCountry = m_config.defaultPubCountry;
			}

			// check license
			string licenseTag = GetLicenseTag(author, creator, latestYear, pubCountry);
			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new LicenseException(dateParseMetadata.LatestYear, pubCountry);
			}

			if (dateParseMetadata.LatestYear < 1897 && author == "{{Creator:Asahel Curtis}}")
			{
				throw new UWashException("Curtis - check author");
			}

			//======== BUILD PAGE TEXT

			StringBuilder content = new StringBuilder();

			content.AppendLine(GetCheckCategoriesTag(categories.Count));

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
				content.AppendLine("|photographer=" + author);
			}
			else if (informationTemplate == "Artwork")
			{
				content.AppendLine("|artist=" + author);
			}
			else
			{
				content.AppendLine("|author=" + author);
			}
			if (informationTemplate != "Information")
			{
				content.AppendLine("|title={{" + lang + "|" + metadata["Title"] + "}}");
			}
			content.AppendLine("|description=");
			StringBuilder descText = new StringBuilder();

			string notes = "";
			if (metadata.TryGetValue("Notes", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			if (metadata.TryGetValue("Contextual Notes", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			if (metadata.TryGetValue("Historical Notes", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			if (metadata.TryGetValue("Scrapbook Notes", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			descText.AppendLine("<p>" + notes + "</p>");

			if (metadata.TryGetValue("Geographic Coverage", out temp))
			{
				descText.AppendLine("*Geographic coverage: " + temp.Trim());
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

			if (descText.Length > 0)
			{
				content.AppendLine("{{en|");
				content.Append(descText.ToString());
				content.AppendLine("}}");
			}

			if (!string.IsNullOrEmpty(sureLocationCategory))
			{
				if (sureLocationCategory.StartsWith("Category:")) sureLocationCategory = sureLocationCategory.Substring("Category:".Length);
				content.AppendLine("|depicted place=" + sureLocationCategory);
			}
			else if (metadata.TryGetValue("Location Depicted", out temp))
			{
				content.AppendLine("|depicted place={{" + lang + "|" + temp + "}}");
			}

			string placeOfCreation;
			if (metadata.TryGetValue("Place of Publication", out placeOfCreation))
			{
				IEnumerable<string> locCats = CategoryTranslation.TranslateLocationCategory(placeOfCreation);
				string placeOfCreationCat = locCats == null ? "" : locCats.FirstOrDefault();
				if (placeOfCreationCat.StartsWith("Category:")) placeOfCreationCat = placeOfCreationCat.Substring("Category:".Length);
				if (!string.IsNullOrEmpty(placeOfCreationCat))
				{
					content.AppendLine("|place of creation=" + placeOfCreationCat);
				}
				else
				{
					content.AppendLine("|place of creation={{" + lang + "|" + placeOfCreation + "}}");
				}
			}

			string caption;
			if (metadata.TryGetValue("Caption Text", out caption))
			{
				caption = caption.Replace("|", "<br/>\n");
				content.AppendLine("|inscriptions={{inscription|language=en|" + caption + "}}");
			}

			content.AppendLine("|date=" + dateTag);
			if (metadata.ContainsKey("Physical Description"))
			{
				string medium;
				Dimensions dimensions;
				ParsePhysicalDescription(metadata["Physical Description"], out medium, out dimensions);
				if (!string.IsNullOrEmpty(medium))
				{
					content.AppendLine("|medium={{en|" + medium + "}}");
				}
				if (!dimensions.IsEmpty)
				{
					// check the dimensions against the image
					//TODO:

					content.AppendLine("|dimensions=" + dimensions.GetCommonsTag());
				}
			}

			string otherFields = "";

			if (m_config.informationTemplate == "Information")
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Institution|value={{Institution:University of Washington}}}}");
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Department|value={{UWASH-Special-Collections}}}}");
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Accession number|value={{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}}}");
			}
			else
			{
				content.AppendLine("|institution={{Institution:University of Washington}}");
				content.AppendLine("|department={{UWASH-Special-Collections}}");
				content.AppendLine("|accession number={{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}");
			}

			content.AppendLine("|source=" + UWashConfig.sourceTemplate); //was department, for Artwork
			content.AppendLine("|permission=" + licenseTag);

			if (metadata.TryGetValue("Advertisement Text", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Inscriptions|value={{Inscription|" + temp + "|type=text|language=en}}}}");
			}
			if (metadata.TryGetValue("Publisher", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher|value=" + temp + "}}");
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
			if (metadata.TryGetValue("Company/Advertising Agency", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Company/Advertising Agency|value=" + temp + "}}");
			}
			if (!string.IsNullOrEmpty(publisherLocation))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher Location|value=" + publisherLocation + "}}");
			}
			if (metadata.TryGetValue("Publication Source", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publication Source|value=" + temp + "}}");
			}

			content.AppendLine("|other_versions=");

			if (!string.IsNullOrEmpty(otherFields))
			{
				content.AppendLine("|other_fields=\n" + otherFields);
			}

			content.AppendLine("}}");
			content.AppendLine();
			/*if (latestYear == 1923)
			{
				content.AppendLine("[[Category:Media uploaded for Public Domain Day 2019]]");
			}*/
			if (!string.IsNullOrEmpty(m_config.checkCategory))
			{
				if (!m_config.checkCategory.StartsWith("Category:"))
				{
					m_config.checkCategory = "Category:" + m_config.checkCategory;
				}
				content.AppendLine("[[" + m_config.checkCategory + "]]");
			}
			if (m_config.additionalCategories != null)
			{
				foreach (string category in m_config.additionalCategories)
				{
					content.AppendLine("[[" + category + "]]");
				}
			}
			if (creator != null && !string.IsNullOrEmpty(creator.Category))
			{
				content.AppendLine("[[" + creator.Category + "]]");
			}

			foreach (string s in categories)
			{
				if (!s.StartsWith("Category:"))
					content.AppendLine("[[Category:" + s + "]]");
				else
					content.AppendLine("[[" + s + "]]");
			}

			if (latestYear == 9999)
			{
				throw new UWashException("unknown year");
			}

			//TODO: this will pass if one of the multiple authors was matched. Fix it.
			if ((creator == null || string.IsNullOrEmpty(creator.Author))
				&& !m_config.allowFailedCreators)
			{
				if (creator != null)
				{
					creator.UploadableUsage++;
				}
				throw new UWashException("unrecognized creator");
			}

			return content.ToString();
		}

		protected override bool TryAddDuplicate(string targetPage, string key, Dictionary<string, string> metadata)
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
			return Api.SetPage(targetArticle, "(BOT) recording duplicate image from UWash collection", false, true);
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
		private string GetAuthor(Dictionary<string, string> data, string lang, out Creator creator)
		{
			string notes;
			if (data.TryGetValue("Notes", out notes)
				&& notes.Contains("Original photographer unknown"))
			{
				creator = null;
				return "{{unknown|author}}";
			}

			string author;
			//TODO: support multiples
			if (data.TryGetValue("Original Creator", out author)
				|| data.TryGetValue("Photographer", out author)
				|| data.TryGetValue("Company/Advertising Agency", out author)
				|| data.TryGetValue("Publisher", out author))
			{
				return GetAuthor(author, lang, out creator);
			}
			else
			{
				return GetAuthor(m_config.defaultAuthor, "en", out creator);
			}
		}

		private string GetDate(Dictionary<string, string> data, out DateParseMetadata parseMetadata)
		{
			string date;
			if (!data.TryGetValue("Date", out date)
				&& !data.TryGetValue("Publication Date", out date))
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
						string titleDate = DateUtility.ParseDate(possibleDate, out titleParseMetadata);
						if (titleParseMetadata.LatestYear == parseMetadata.LatestYear)
						{
							return titleDate;
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
	}
}