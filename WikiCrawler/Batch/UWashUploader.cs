using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WikiCrawler;

namespace UWash
{
	public class UWashUploader : BatchUploader
	{
		private static HashSet<string> s_knownKeys = new HashSet<string>()
		{
			//used
			"title", //Title
			"creato", //Photographer
			"date", //Date
			"descri", //Notes
			"histori", //Contextual Notes
			"histor", //Contextual Notes
			"struct", //Contextual Notes
			"scrapb", //Scrapbook Notes
			"albump", //Album/Page
			"subjec", //Subjects (LCTGM)
			"lctgm", //Subjects (LCTGM)
			"subjea", //Subjects (LCSH)
			"covera", //Location Depicted
			"order", //Order Number
			"physic", //Physical Description
			"publis", //Company/Advertising Agency
			"publia", //Publisher
			"type", //Publication Source
			"place", //Publisher Location
			"compan", //Geographic Coverage

			"LCSH",
			"LCTGM",

			// unused
			"format", //Digital Reproduction Information
			"righta", //Rights URI
			"rights", //Restrictions
			"object", //Object Type
			"objeca", //Object Type
			"orderi", //Ordering Information
			"citati", //Citation Information
			"reposi", //Repository
			"conten", //Repository
			"source", //Repository Collection
			"digita", //Digital Collection
			"langua", //Digital Collection
			"sourca", //Source
			"negati", //Negative Number
			"identi", //Photographer's Reference Number
			"digitb", //Digital ID Number
		};

		private UWashProjectConfig UWashConfig
		{
			get { return (UWashProjectConfig)m_config; }
		}

		private static char[] s_punctuation = { '.', ' ', '\t', '\n', ',', '-' };
		private static string[] s_categorySplitters = { "|", ";", "<br/>", "<br />", "<br>" };

		private static WebClient s_client = new WebClient();

		public UWashUploader(string key, UWashProjectConfig config)
			: base(key, config)
		{

		}

		/// <summary>
		/// Returns the title of the uploaded page for the specified metadata.
		/// </summary>
		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			string title = metadata["Title"];

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
			title = title.Replace("/", "") + " (" + m_config.filenameSuffix + " " + key + ")";

			return title;
		}

		/// <summary>
		/// Prepares the image for upload and returns the path to the file to upload.
		/// </summary>
		protected override string GetUploadImagePath(string key, Dictionary<string, string> metadata)
		{
			if (!m_config.allowImageDownload)
			{
				throw new UWashException("image download disabled");
			}

			//Download image
			string imagepath = GetImageCacheFilename(key);
			if (!File.Exists(imagepath))
			{
				Console.WriteLine("Downloading image.");
				Uri uri = new Uri(GetImageUrl(key));
				EasyWeb.WaitForDelay(uri);
				s_client.DownloadFile(uri, imagepath);
			}
			else
			{
				Console.WriteLine("Found cached image.");
			}

			// try to crop the image
			string croppath = GetImageCroppedFilename(key);
			ImageUtils.CropUwashWatermark(imagepath, croppath);
			if (UWashConfig.allowCrop)
			{
				ImageUtils.AutoCropJpg(croppath, croppath, 0xffffffff, 0.92f, 22, ImageUtils.Side.Left | ImageUtils.Side.Right);
				System.Threading.Thread.Sleep(50);
				if (!File.Exists(croppath))
				{
					throw new UWashException("crop failed");
				}
			}

			return croppath;
		}

		/// <summary>
		/// Builds the wiki page for the object with the specified metadata.
		/// </summary>
		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
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
			/*if (data.ContainsKey("Caption"))
			{
				//max 50
				foreach (string s in data["Caption"].Split(captionSplitters, StringSplitOptions.RemoveEmptyEntries))
				{
					if (s.Length < 50) catparse += "|" + s;
				}
			}*/
			foreach (string s in catparse.Split(s_categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				string cat = CategoryTranslation.TranslateTagCategory(s.Trim());
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
			}

			//categories for locations
			string sureLocation = "";
			catparse = "";
			if (metadata.TryGetValue("covera", out temp)) catparse += "|" + temp;
			foreach (string s in catparse.Split(s_categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				string cat = CategoryTranslation.TranslateLocationCategory(s.Trim());
				if (!string.IsNullOrEmpty(cat) && cat != "N/A")
				{
					foreach (string catsplit in cat.Split('|'))
					{
						if (string.IsNullOrEmpty(sureLocation)) sureLocation = catsplit;
						if (!categories.Contains(catsplit)) categories.Add(catsplit);
					}
				}
			}

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			int latestYearRaw;
			string dateTag = GetDate(metadata, out latestYearRaw);

			// must have been published before author's death
			int latestYear;
			if (creator != null && latestYearRaw == 9999)
			{
				latestYear = creator.DeathYear;
			}
			else
			{
				latestYear = latestYearRaw;
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
			string licenseTag = "";
			if (author == "{{unknown|author}}" || author == "{{anonymous}}")
			{
				licenseTag = LicenseUtility.GetPdLicenseTagUnknownAuthor(latestYear, pubCountry);
			}
			else if (creator != null)
			{
				if (!string.IsNullOrEmpty(creator.LicenseTemplate))
				{
					licenseTag = creator.LicenseTemplate;
				}
				else
				{
					licenseTag = LicenseUtility.GetPdLicenseTag(latestYear, creator.DeathYear, pubCountry);
				}
			}
			else
			{
				licenseTag = LicenseUtility.GetPdLicenseTag(latestYear, null, pubCountry);
			}
			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new UWashException("not PD? (pub: " + pubCountry + " " + latestYearRaw + ")");
			}

			if (latestYearRaw < 1897 && author == "{{Creator:Asahel Curtis}}")
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
			if (metadata.TryGetValue("descri", out temp))
			{
				//TEMP: pipe replace is temp (only used in napoleon)
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim().Replace("|", "</p>\n<p>"));
			}
			if (metadata.TryGetValue("histori", out temp))
			{
				//TEMP: pipe replace is temp (only used in napoleon)
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim().Replace("|", "</p>\n<p>"));
			}
			if (metadata.TryGetValue("histor", out temp))
			{
				//TEMP: pipe replace is temp (only used in napoleon)
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim().Replace("|", "</p>\n<p>"));
			}
			if (metadata.TryGetValue("struct", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			if (metadata.TryGetValue("scrapb", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			descText.AppendLine("<p>" + notes + "</p>");

			if (metadata.TryGetValue("compan", out temp))
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

			if (descText.Length > 0)
			{
				content.AppendLine("{{en|");
				content.Append(descText.ToString());
				content.AppendLine("}}");
			}

			if (!string.IsNullOrEmpty(sureLocation))
			{
				if (sureLocation.StartsWith("Category:")) sureLocation = sureLocation.Substring("Category:".Length);
				content.AppendLine("|depicted place=" + sureLocation);
			}
			else if (metadata.TryGetValue("covera", out temp))
			{
				content.AppendLine("|depicted place={{" + lang + "|" + temp + "}}");
			}

			string placeOfCreation;
			if (metadata.TryGetValue("Place of Publication", out placeOfCreation))
			{
				string placeOfCreationCat = CategoryTranslation.TranslateLocationCategory(placeOfCreation)
					.Split(StringUtility.Pipe)
					.FirstOrDefault();
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
			if (metadata.ContainsKey("physic"))
			{
				string medium;
				Dimensions dimensions;
				ParsePhysicalDescription(metadata["physic"], out medium, out dimensions);
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

			//TODO: need to be Information Field for Information
			content.AppendLine("|institution={{Institution:University of Washington}}");
			content.AppendLine("|department={{UWASH-Special-Collections}}");
			content.AppendLine("|accession number={{UWASH-digital-accession|" + UWashConfig.digitalCollectionsKey + "|" + key + "}}");

			content.AppendLine("|source=" + UWashConfig.sourceTemplate); //was department, for Artwork

			content.AppendLine("|permission=" + licenseTag);

			string otherFields = "";

			if (metadata.TryGetValue("Publisher", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("order", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Order Number|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("albump", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Album/Page|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("digitb", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Digital ID Number|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("UW Reference Number", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=UW Reference Number|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("publis", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Company/Advertising Agency|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("publia", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("place", out temp))
			{
				temp = CategoryTranslation.TranslateLocationCategory(temp);
				if (temp.StartsWith("Category:")) temp = temp.Substring("Category:".Length);
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher Location|value=" + temp + "}}");
			}
			if (metadata.TryGetValue("type", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Original Source|value=" + temp + "}}");
			}

			if (!string.IsNullOrEmpty(otherFields))
			{
				content.AppendLine("|other_fields=\n" + otherFields);
			}

			content.AppendLine("}}");
			content.AppendLine();
			if (latestYear == 1923)
			{
				content.AppendLine("[[Category:Media uploaded for Public Domain Day 2019]]");
			}
			if (!string.IsNullOrEmpty(m_config.checkCategory))
			{
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

			string previewFile = Path.Combine(PreviewDirectory, key + ".txt");
			using (StreamWriter writer = new StreamWriter(new FileStream(previewFile, FileMode.Create)))
			{
				writer.Write(content.ToString());
			}

			if (latestYear >= 9999)
			{
				throw new UWashException("unknown year");
			}

			return content.ToString();
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
			if (data.TryGetValue("descri", out notes)
				&& notes.Contains("Original photographer unknown"))
			{
				creator = null;
				return "{{unknown|author}}";
			}

			string author;
			//TODO: support multiples
			if (data.TryGetValue("creato", out author)
				|| data.TryGetValue("publis", out author)
				|| data.TryGetValue("publia", out author))
			{
				return GetAuthor(author, lang, out creator);
			}
			else
			{
				return GetAuthor(m_config.defaultAuthor, "en", out creator);
			}
		}

		private string GetDate(Dictionary<string, string> data, out int latestYear)
		{
			string date;
			if (!data.TryGetValue("date", out date))
			{
				latestYear = 9999;
				return "{{unknown|date}}";
			}

			date = ParseDate(date, out latestYear);

			// if the date is just the year, there may be a more precise date at the end of the title
			if (date == latestYear.ToString())
			{
				string title;
				if (data.TryGetValue("title", out title))
				{
					int commaIndex = title.IndexOf(',');
					while (commaIndex >= 0)
					{
						string possibleDate = title.Substring(commaIndex + 1).Trim();
						int titleLatestYear;
						string titleDate = ParseDate(possibleDate, out titleLatestYear);
						if (titleLatestYear == latestYear)
						{
							return titleDate;
						}
						commaIndex = title.IndexOf(',', commaIndex + 1);
					}
				}
			}

			return date;
		}

		private static string GetDesc(Dictionary<string, string> metadata)
		{
			string title;
			if (!metadata.TryGetValue("title", out title))
			{
				throw new UWashException("no title");
			}

			List<string> raw = title.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries).ToList();
			string temp;
			if (metadata.TryGetValue("descri", out temp))
			{
				raw.AddRange(temp.Split(StringUtility.LineBreak, StringSplitOptions.RemoveEmptyEntries));
			}
			if (metadata.TryGetValue("histori", out temp))
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
