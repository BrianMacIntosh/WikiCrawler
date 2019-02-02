using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Globalization;
using WikiCrawler;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace UWash
{
	static class UWashController
	{
		private static string url
		{
			get { return "http://digitalcollections.lib.washington.edu/cdm/singleitem/collection/" + projectConfig.digitalCollectionsKey + "/id/{0}"; }
		}

		private static string imageUrl
		{
			get
			{
				return "http://digitalcollections.lib.washington.edu/utils/ajaxhelper/?CISOROOT="
					+ projectConfig.digitalCollectionsKey
					+ "&action=2&CISOPTR={0}&DMSCALE=100&DMWIDTH=99999&DMHEIGHT=99999&DMX=0&DMY=0&DMTEXT=&REC=1&DMTHUMB=0&DMROTATE=0";
			}
		}

		private static Wikimedia.WikiApi Api = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));
		private static Wikimedia.WikiApi WikidataApi = new Wikimedia.WikiApi(new Uri("http://wikidata.org/"));
		private static Wikimedia.CategoryTree CategoryTree = new Wikimedia.CategoryTree(Api);

		private static List<string> knownKeysNew = new List<string>()
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

		private static Dictionary<string, string> newKeysToOld = new Dictionary<string, string>()
		{
			{ "title",   "Title" },
			{ "creato",  "Photographer" },
			{ "date",    "Date" },
			{ "descri",  "Notes" },
			{ "histori", "Contextual Notes" },
			{ "scrapb",  "Scrapbook Notes" },
			{ "albump",  "Album/Page" },
			{ "subjec",  "Subjects (LCTGM)" },
			{ "subjea",  "Subjects (LCSH)" },
			{ "covera",  "Location Depicted" },
			{ "order",   "Order Number" },
			{ "physic",  "Physical Description" },
			{ "format",  "Digital Reproduction Information" },
			{ "righta",  "Rights URI" },
			{ "rights",  "Restrictions" },
			{ "object",  "Object Type" },
			{ "orderi",  "Ordering Information" },
			{ "citati",  "Citation Information" },
			{ "reposi",  "Repository" },
			{ "source",  "Repository Collection" },
			{ "digita",  "Digital Collection" },
			{ "sourca",  "Source" },
			{ "negati",  "Negative Number" },
			{ "identi",  "Photographer's Reference Number" },
			{ "digitb",  "Digital ID Number" },
		};

		private static Dictionary<string, string> oldKeysToNew = new Dictionary<string, string>()
		{
			{ "LCSH", "subjea" },
			{ "LCTGM", "subjec" }
		};

		private static WebClient client = new WebClient();
		private static string[] captionSplitters = new string[] { "--", "|" };
		private static char[] punctuation = { '.', ' ', '\t', '\n', ',', '-' };
		private static string[] categorySplitters = { "|", ";", "<br/>", "<br />", "<br>" };
		private static char[] pipe = { '|' };
		private static string[] lineBreak = { "|", "<br/>", "<br />", "<br>", "\r\n", "\n" }; //TEMP: pipe is temp (napoleon only)
		private static char[] colon = { ':' };
		public static char[] parens = { '(', ')', ' ' };
		private static string[] dashdash = new string[] { "--" };
		private static char[] equals = new char[] { '=' };

		private static Dictionary<string, string> creatorHomecats = new Dictionary<string, string>();

		private static Dictionary<string, string> categoryMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

		private static Dictionary<string, UWashCreator> creatorData = new Dictionary<string, UWashCreator>();

		//parsed data for the current document
		private static Dictionary<string, string> data = new Dictionary<string, string>();
		private static bool parseSuccessful = false;

		//configuration
		private static UWashConfig config;
		private static int newCount = 0;
		private static int failedCount = 0;
		private static int succeededCount = 0;

		private static UWashProjectConfig projectConfig;
		private static string projectKey;
		private static string projectDir;

		private static string imagesDirectory
		{
			get { return Path.Combine(projectDir, "images"); }
		}
		private static string dataCacheDirectory
		{
			get { return Path.Combine(projectDir, "data_cache"); }
		}
		private static string previewDirectory
		{
			get { return Path.Combine(projectDir, "preview"); }
		}

		private static int current;
		private static int saveOutCounter = 0;
		private static List<UWashFailure> failures = new List<UWashFailure>();

		private static bool Initialize()
		{
			string homeConfigFile = Path.Combine(Configuration.DataDirectory, "config.json");
			config = Newtonsoft.Json.JsonConvert.DeserializeObject<UWashConfig>(
				File.ReadAllText(homeConfigFile, Encoding.UTF8));

			Console.Write("Project Key>");
			projectKey = Console.ReadLine();
			projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return false;
			}

			string projectConfigFile = Path.Combine(projectDir, "config.json");
			projectConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<UWashProjectConfig>(
				File.ReadAllText(projectConfigFile, Encoding.UTF8));

			string failedFile = Path.Combine(projectDir, "failed.json");
			if (File.Exists(failedFile))
			{
				failures = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UWashFailure>>(
					File.ReadAllText(failedFile, Encoding.UTF8));
			}

			foreach (KeyValuePair<string, string> kv in newKeysToOld)
			{
				oldKeysToNew[kv.Value] = kv.Key;
			}

			return true;
		}

		public static void RebuildFailures()
		{
			if (!Initialize())
			{
				return;
			}

			Console.WriteLine("Finding holes...");
			List<int> missing = new List<int>();
			for (int i = projectConfig.minIndex; i <= projectConfig.maxIndex; i++)
			{
				missing.Add(i);
			}

			string suffixStart = " (" + projectConfig.filenameSuffix + " ";
			foreach (Wikimedia.Article article in Api.GetCategoryFiles(projectConfig.masterCategory))
			{
				Console.WriteLine(article.title);
				int tagIndex = article.title.IndexOf(suffixStart);
				if (tagIndex < 0)
				{
					continue;
				}
				int numStart = tagIndex + suffixStart.Length;
				int numEnd = article.title.IndexOf(')', numStart);
				int articleId = int.Parse(article.title.Substring(numStart, numEnd - numStart));
				missing.Remove(articleId);
			}

			string failedFile = Path.Combine(projectDir, "failed.json");
			using (StreamWriter writer = new StreamWriter(new FileStream(failedFile, FileMode.Create)))
			{
				writer.WriteLine("[");
				bool first = true;
				foreach (int i in missing)
				{
					if (!first) writer.WriteLine(",");
					writer.Write("\t{ \"Index\": " + i + ", \"Reason\": \"missing from commons\" }");
					first = false;
				}
				writer.WriteLine("]");
			}
		}

		/// <summary>
		/// Removes cached metadata for files that are already uploaded.
		/// </summary>
		public static void CacheCleanup()
		{
			if (!Initialize())
			{
				return;
			}

			int count = 0;
			foreach (string s in Directory.GetFiles(dataCacheDirectory))
			{
				int fileId = int.Parse(Path.GetFileNameWithoutExtension(s));
				if (!failures.Any(failure => failure.Index == fileId))
				{
					File.Delete(s);
					count++;
				}
			}
			Console.WriteLine("Deleted cached data: " + count);

			count = 0;
			foreach (string s in Directory.GetFiles(previewDirectory))
			{
				int fileId = int.Parse(Path.GetFileNameWithoutExtension(s));
				if (!failures.Any(failure => failure.Index == fileId))
				{
					File.Delete(s);
					count++;
				}
			}
			Console.WriteLine("Deleted preview data: " + count);
		}

		private static void ValidateCreators()
		{
			Console.WriteLine("Validating creators...");
			foreach (KeyValuePair<string, UWashCreator> kv in creatorData)
			{
				UWashCreator data = kv.Value;
				Console.WriteLine(kv.Key);

				string attempt = kv.Key;

				if (attempt.StartsWith("Creator:"))
					attempt = attempt.Substring("Creator:".Length);

				if (string.IsNullOrEmpty(data.Author) && !string.IsNullOrEmpty(attempt))
				{
					//1. validate suggested creator
					//Wikimedia.Article creatorArt = Api.GetPage("Creator:" + attempt);
					//if (creatorArt != null && !creatorArt.missing)
					//{
					//	data.Author = "{{" + creatorArt.title + "}}";
					//}

					//2. try to create suggested creator
					if (string.IsNullOrEmpty(data.Author))
					{
						//string trying = attempt;
						//Console.WriteLine("Attempting creation...");
						//if (CommonsCreatorFromWikidata.TryMakeCreator(Api, ref trying))
						//	data.Succeeeded = trying;
					}
				}
			}
		}

		public static void Harvest()
		{
			if (!Initialize())
			{
				return;
			}

			EasyWeb.SetDelayForDomain(new Uri(imageUrl), 30f);

			//load progress
			string progressFile = Path.Combine(projectDir, "progress.dat");
			if (File.Exists(progressFile))
			{
				using (BinaryReader reader = new BinaryReader(new FileStream(progressFile, FileMode.Open)))
				{
					current = reader.ReadInt32();
				}
			}
			else
			{
				current = projectConfig.minIndex;
			}

			//load mapped categories
			string categoryMappingsFile = Path.Combine(Configuration.DataDirectory, "category_mappings.txt");
			if (File.Exists(categoryMappingsFile))
			{
				using (StreamReader reader = new StreamReader(new FileStream(categoryMappingsFile, FileMode.Open)))
				{
					while (!reader.EndOfStream)
					{
						string[] line = reader.ReadLine().Split(pipe, 2);
						if (line.Length == 2)
						{
							categoryMap[line[0]] = line[1];
						}
					}
				}
			}

			//load known creators
			string creatorTemplatesFile = Path.Combine(projectDir, "creator_templates.json");
			if (File.Exists(creatorTemplatesFile))
			{
				creatorData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, UWashCreator>>(
					File.ReadAllText(creatorTemplatesFile, Encoding.UTF8));
				foreach (UWashCreator creator in creatorData.Values)
				{
					creator.Usage = 0;
				}
			}

			CategoryTree.Load(Path.Combine(Configuration.DataDirectory, "category_tree.txt"));

			if (!Directory.Exists(imagesDirectory))
				Directory.CreateDirectory(imagesDirectory);

			if (!Directory.Exists(dataCacheDirectory))
				Directory.CreateDirectory(dataCacheDirectory);

			if (!Directory.Exists(previewDirectory))
				Directory.CreateDirectory(previewDirectory);

			Console.WriteLine("Logging in...");
			Credentials credentials = Configuration.LoadCredentials();
			Api.LogIn(credentials.Username, credentials.Password);

			ValidateCreators();

			string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");
			try
			{
				//Try to reprocess old things
				Console.WriteLine();
				Console.WriteLine("Begin reprocessing of previously failed uploads.");
				Console.WriteLine();

				for (int c = 0; c < failures.Count && failedCount < config.maxFailed && succeededCount < config.maxSuccesses; c++)
				{
					Console.WriteLine();
					data.Clear();
					parseSuccessful = false;

					UWashFailure fail = failures[c];
					try
					{
						Process(fail.Index);

						//If we made it here, we succeeded
						failures.RemoveAt(c);
						c--;
					}
					catch (UWashException e)
					{
						Console.WriteLine("REFAILED:" + e.Message);
						failures[c] = new UWashFailure(fail.Index, e.Message);
					}

					failedCount++;

					saveOutCounter++;
					if (saveOutCounter >= config.saveOutInterval)
					{
						SaveOut();
						saveOutCounter = 0;
					}

					if (File.Exists(stopFile))
					{
						File.Delete(stopFile);
						return;
					}
				}

				//Process new things
				Console.WriteLine();
				Console.WriteLine("Begin processing new files.");
				Console.WriteLine();
				for (; current <= projectConfig.maxIndex && newCount < config.maxNew && succeededCount < config.maxSuccesses; )
				{
					Console.WriteLine();
					data.Clear();
					parseSuccessful = false;

					try
					{
						Process(current);
					}
					catch (UWashException e)
					{
						//There was an error
						string failReason = "";
						if (!parseSuccessful) failReason += "PARSE FAIL|";
						failReason += e.Message;
						failures.Add(new UWashFailure(current, failReason));
						Console.WriteLine("ERROR:" + e.Message);
					}

					current++;

					newCount++;

					saveOutCounter++;
					if (saveOutCounter >= config.saveOutInterval)
					{
						SaveOut();
						saveOutCounter = 0;
					}

					if (File.Exists(stopFile))
					{
						File.Delete(stopFile);
						Console.WriteLine("Got stop message.");
						return;
					}
				}
			}
			finally
			{
				SaveOut();
			}
		}

		/// <summary>
		/// Write out current progress to files.
		/// </summary>
		private static void SaveOut()
		{
			//write progress
			string progressFile = Path.Combine(projectDir, "progress.dat");
			using (BinaryWriter writer = new BinaryWriter(new FileStream(progressFile, FileMode.Create)))
			{
				writer.Write(current);
			}

			//write category map
			string categoryMappingsFile = Path.Combine(Configuration.DataDirectory, "category_mappings.txt");
			using (StreamWriter writer = new StreamWriter(new FileStream(categoryMappingsFile, FileMode.Create)))
			{
				foreach (KeyValuePair<string, string> kv in categoryMap)
				{
					writer.WriteLine(kv.Key + "|" + kv.Value);
				}
			}

			//write errors
			string failedFile = Path.Combine(projectDir, "failed.json");
			File.WriteAllText(
				failedFile,
				Newtonsoft.Json.JsonConvert.SerializeObject(failures, Newtonsoft.Json.Formatting.Indented),
				Encoding.UTF8);

			//write creators
			string creatorTemplatesFile = Path.Combine(projectDir, "creator_templates.json");
			File.WriteAllText(
				creatorTemplatesFile,
				Newtonsoft.Json.JsonConvert.SerializeObject(creatorData, Newtonsoft.Json.Formatting.Indented),
				Encoding.UTF8);

			CategoryTree.Save(Path.Combine(Configuration.DataDirectory, "category_tree.txt"));
		}

		private static void Process(int current)
		{
			Console.WriteLine("BEGIN:" + current);

			//get metadata
			string metacache = GetMetaCacheFilename(current);
			string metacacheText = Path.ChangeExtension(metacache, "txt");

			if (File.Exists(metacache))
			{
				Console.WriteLine("Found cached metadata.");
				data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(metacache, Encoding.UTF8));

				// repair old metadata keys
				foreach (string key in data.Keys.ToArray())
				{
					string newKey;
					if (oldKeysToNew.TryGetValue(key, out newKey))
					{
						data[newKey] = data[key];
						data.Remove(key);
					}
				}
			}
			else
			{
				if (File.Exists(metacacheText))
				{

				}
				else if (!projectConfig.allowDataDownload)
				{
					throw new UWashException("redownload");
				}

				// retrieve and parse the data from the web
				Console.WriteLine("Downloading metadata.");
				if (!ReadMetadata(current, data))
				{
					return;
				}

				// cache the metadata
				File.WriteAllText(metacache, Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
			}
			parseSuccessful = true;

			data = PreprocessMetadata(data);

			//check for errors that will need inspection
			string title;
			if (!data.TryGetValue("title", out title))
			{
				throw new UWashException("no title");
			}

			//check for new, unknown metadata
			string unused = "";
			foreach (KeyValuePair<string, string> kv in data)
			{
				string key = kv.Key;
				if (!knownKeysNew.Contains(key))
				{
					unused += "|" + kv.Key;
				}
			}
			if (!string.IsNullOrEmpty(unused)) throw new UWashException("unused key|" + unused);

			string captionTitle = title.Split(lineBreak, StringSplitOptions.RemoveEmptyEntries)[0].Trim(punctuation).Replace(".", "");
			if (captionTitle.Length > 129)
			{
				//truncate the title to 128 characters on a word boundary
				int lastWordEnd = 0;
				for (int c = 0; c < 128; c++)
				{
					if (!char.IsLetterOrDigit(captionTitle[c+1]) && char.IsLetterOrDigit(captionTitle[c]))
						lastWordEnd = c;
				}
				captionTitle = captionTitle.Remove(lastWordEnd + 1);
			}
			Wikimedia.Article art = new Wikimedia.Article();
			art.title = captionTitle.Replace("/", "") + " (" + projectConfig.filenameSuffix + " " + current + ")";
			art.revisions = new Wikimedia.Revision[1];
			art.revisions[0] = new Wikimedia.Revision();

			string lang = "en";

			List<string> categories = new List<string>();
			string catparse = "";

			UWashCreator creator;
			string author = GetAuthor(data, lang, out creator);

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
			if (data.TryGetValue("LCTGM", out temp)) catparse += "|" + temp;
			if (data.TryGetValue("LCSH", out temp)) catparse += "|" + temp;
			/*if (data.ContainsKey("Caption"))
			{
				//max 50
				foreach (string s in data["Caption"].Split(captionSplitters, StringSplitOptions.RemoveEmptyEntries))
				{
					if (s.Length < 50) catparse += "|" + s;
				}
			}*/
			foreach (string s in catparse.Split(categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				string cat = TranslateTagCategory(s.Trim());
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
			if (data.TryGetValue("covera", out temp)) catparse += "|" + temp;
			foreach (string s in catparse.Split(categorySplitters, StringSplitOptions.RemoveEmptyEntries))
			{
				string cat = TranslateLocationCategory(s.Trim());
				if (!string.IsNullOrEmpty(cat) && cat != "N/A")
				{
					foreach (string catsplit in cat.Split('|'))
					{
						if (string.IsNullOrEmpty(sureLocation)) sureLocation = catsplit;
						if (!categories.Contains(catsplit)) categories.Add(catsplit);
					}
				}
			}

			CategoryTree.RemoveLessSpecific(categories);

			int latestYearRaw;
			string dateTag = GetDate(data, out latestYearRaw);

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
			if (data.TryGetValue("Place of Publication", out pubCountry))
			{
				pubCountry = UWashCountry.Parse(pubCountry);
			}
			else
			{
				pubCountry = projectConfig.defaultPubCountry;
			}

			// check license
			string licenseTag = "";
			if (author == "{{unknown|author}}" || author == "{{anonymous}}")
			{
				licenseTag = GetPdLicenseTagUnknownAuthor(latestYear, pubCountry);
			}
			else if (creator != null)
			{
				if (!string.IsNullOrEmpty(creator.LicenseTemplate))
				{
					licenseTag = creator.LicenseTemplate;
				}
				else
				{
					licenseTag = GetPdLicenseTag(latestYear, creator.DeathYear, pubCountry);
				}
			}
			else
			{
				licenseTag = GetPdLicenseTag(latestYear, null, pubCountry);
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

			string informationTemplate = projectConfig.informationTemplate;
			if (data.ContainsKey("~art"))
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
				content.AppendLine("|title={{" + lang + "|" + title + "}}");
			}
			content.AppendLine("|description=");
			StringBuilder descText = new StringBuilder();

			string notes = "";
			if (data.TryGetValue("descri", out temp))
			{
				//TEMP: pipe replace is temp (only used in napoleon)
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim().Replace("|", "</p>\n<p>"));
			}
			if (data.TryGetValue("histori", out temp))
			{
				//TEMP: pipe replace is temp (only used in napoleon)
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim().Replace("|", "</p>\n<p>"));
			}
			if (data.TryGetValue("histor", out temp))
			{
				//TEMP: pipe replace is temp (only used in napoleon)
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim().Replace("|", "</p>\n<p>"));
			}
			if (data.TryGetValue("struct", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			if (data.TryGetValue("scrapb", out temp))
			{
				notes = StringUtility.Join("</p>\n<p>", notes, temp.Trim());
			}
			descText.AppendLine("<p>" + notes + "</p>");

			if (data.TryGetValue("compan", out temp))
			{
				descText.AppendLine("*Geographic coverage: " + temp.Trim());
			}
			if (data.TryGetValue("LCTGM", out temp))
			{
				descText.AppendLine("*Subjects (LCTGM): " + temp.Replace("|", "; "));
			}
			if (data.TryGetValue("LCSH", out temp))
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
			else if (data.TryGetValue("covera", out temp))
			{
				content.AppendLine("|depicted place={{" + lang + "|" + temp + "}}");
			}

			string placeOfCreation;
			if (data.TryGetValue("Place of Publication", out placeOfCreation))
			{
				string placeOfCreationCat = TranslateLocationCategory(placeOfCreation).Split(pipe).FirstOrDefault();
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
			if (data.TryGetValue("Caption Text", out caption))
			{
				caption = caption.Replace("|", "<br/>\n");
				content.AppendLine("|inscriptions={{inscription|language=en|" + caption + "}}");
			}
			
			content.AppendLine("|date=" + dateTag);
			if (data.ContainsKey("physic"))
			{
				string medium;
				Dimensions dimensions;
				ParsePhysicalDescription(data["physic"], out medium, out dimensions);
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
			content.AppendLine("|accession number={{UWASH-digital-accession|" + projectConfig.digitalCollectionsKey + "|" + current + "}}");

			content.AppendLine("|source=" + projectConfig.sourceTemplate); //was department, for Artwork
			
			content.AppendLine("|permission=" + licenseTag);

			string otherFields = "";

			if (data.TryGetValue("Publisher", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher|value=" + temp + "}}");
			}
			if (data.TryGetValue("order", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Order Number|value=" + temp + "}}");
			}
			if (data.TryGetValue("albump", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Album/Page|value=" + temp + "}}");
			}
			if (data.TryGetValue("digitb", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Digital ID Number|value=" + temp + "}}");
			}
			if (data.TryGetValue("UW Reference Number", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=UW Reference Number|value=" + temp + "}}");
			}
			if (data.TryGetValue("publis", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Company/Advertising Agency|value=" + temp + "}}");
			}
			if (data.TryGetValue("publia", out temp))
			{
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher|value=" + temp + "}}");
			}
			if (data.TryGetValue("place", out temp))
			{
				temp = TranslateLocationCategory(temp);
				if (temp.StartsWith("Category:")) temp = temp.Substring("Category:".Length);
				otherFields = StringUtility.Join("\n", otherFields, "{{Information field|name=Publisher Location|value=" + temp + "}}");
			}
			if (data.TryGetValue("type", out temp))
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
			if (!string.IsNullOrEmpty(projectConfig.checkCategory))
			{
				content.AppendLine("[[" + projectConfig.checkCategory + "]]");
			}
			if (projectConfig.additionalCategories != null)
			{
				foreach (string category in projectConfig.additionalCategories)
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


			art.revisions[0].text = content.ToString();

			using (StreamWriter writer = new StreamWriter(new FileStream(Path.Combine(previewDirectory, current + ".txt"), FileMode.Create)))
			{
				writer.Write(content.ToString());
			}

			if (latestYear >= 9999)
			{
				throw new UWashException("unknown year");
			}

			if (!projectConfig.allowImageDownload)
			{
				throw new UWashException("image download disabled");
			}

			//Download image
			string imagepath = GetImageCacheFilename(current);
			if (!File.Exists(imagepath))
			{
				Console.WriteLine("Downloading image.");
				Uri uri = new Uri(GetImageUrl(current));
				EasyWeb.WaitForDelay(uri);
				client.DownloadFile(uri, imagepath);
			}
			else
			{
				Console.WriteLine("Found cached image.");
			}

			// try to crop the image
			string croppath = GetImageCroppedFilename(current);
			ImageUtils.CropUwashWatermark(imagepath, croppath);
			if (projectConfig.allowCrop)
			{
				ImageUtils.AutoCropJpg(croppath, croppath, 0xffffffff, 0.92f, 22, ImageUtils.Side.Left | ImageUtils.Side.Right);
				System.Threading.Thread.Sleep(50);
				if (!File.Exists(croppath))
				{
					throw new UWashException("crop failed");
				}
			}

			if (!projectConfig.allowUpload)
			{
				throw new UWashException("upload disabled");
			}

			reupload:
			bool uploadSuccess;
			try
			{
				uploadSuccess = Api.UploadFromLocal(art, croppath, "(BOT) bot creating page from UWash database", true);
			}
			catch (WebException e)
			{
				if (e.Status == WebExceptionStatus.ProtocolError
					&& ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.ServiceUnavailable)
				{
					System.Threading.Thread.Sleep(60000);
					goto reupload;
				}
				else
				{
					uploadSuccess = false;
				}
			}

			if (!uploadSuccess)
			{
				throw new UWashException("upload failed");
			}
			else
			{
				//successful upload
				succeededCount++;
				if (File.Exists(imagepath))
				{
					File.Delete(imagepath);
				}
				if (File.Exists(croppath))
				{
					File.Delete(croppath);
				}
				if (File.Exists(metacache))
				{
					File.Delete(metacache);
				}
				if (File.Exists(metacacheText))
				{
					File.Delete(metacacheText);
				}
			}
		}

		public static string GetPdLicenseTagUnknownAuthor(int pubYear, string pubCountry)
		{
			if (UWashCountry.UseEUNoAuthor(pubCountry))
			{
				if (DateTime.Now.Year - pubYear > 70)
				{
					return "{{PD-EU-no author disclosure}}";
				}
				else
				{
					return "";
				}
			}
			else if (pubCountry == "USA")
			{
				if (pubYear <= 1922)
				{
					return "{{PD-anon-expired}}";
				}
				else
				{
					return "";
				}
			}
			else
			{
				return "";
			}
		}

		public static string GetPdLicenseTag(int pubYear, int? authorDeathYear, string pubCountry)
		{
			bool canUsePDOldExpired = false;

			if (pubCountry == "USA")
			{
				canUsePDOldExpired = true;
			}
			else if (authorDeathYear.HasValue)
			{
				canUsePDOldExpired = (DateTime.Now.Year - authorDeathYear.Value) > UWashCountry.GetPMADuration(pubCountry);
			}

			if (canUsePDOldExpired && pubYear < (DateTime.Now.Year - 95))
			{
				if (authorDeathYear.HasValue && authorDeathYear != 9999)
				{
					return "{{PD-old-auto-expired|deathyear=" + authorDeathYear.ToString() + "}}";
				}
				else
				{
					return "{{PD-US-expired}}";
				}
			}
			else
			{
				if (authorDeathYear.HasValue && DateTime.Now.Year - authorDeathYear >= 70)
				{
					return "";// "{{PD-old-auto|deathyear=" + authorDeathYear.ToString() + "}}";
				}
				else
				{
					return "";
				}
			}
		}

		private static string GetAccessionPageUrl(int index)
		{
			return string.Format(url, index);
		}

		private static string GetImageUrl(int index)
		{
			return string.Format(imageUrl, index);
		}

		private static string GetImageCacheFilename(int index)
		{
			return Path.Combine(imagesDirectory, index + ".jpg");
		}

		private static string GetImageCroppedFilename(int index)
		{
			return Path.Combine(imagesDirectory, index + "_cropped.jpg");
		}

		private static string GetMetaCacheFilename(int index)
		{
			return Path.Combine(dataCacheDirectory, index + ".json");
		}

		private static bool ReadMetadata(int current, Dictionary<string, string> data)
		{
			//Read HTML data
			int attempts = 0;
			retryDownload:
			attempts++;
			bool retry = false;
			string contents = "";
			do
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(GetAccessionPageUrl(current)));
				request.UserAgent = "brian@brianmacintosh.com (Wikimedia Commons) - bot";
				try
				{
					using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
					{
						contents = read.ReadToEnd();
					}
				}
				catch (WebException e)
				{
					if (e.Status == WebExceptionStatus.Timeout)
					{
						throw;
						//Console.WriteLine("Tiemout - sleeping (30 sec)");
						//System.Threading.Thread.Sleep(30000);
						//retry = true;
					}
					else
					{
						HttpWebResponse response = (HttpWebResponse)e.Response;
						if (response.StatusCode == HttpStatusCode.NotFound)
						{
							Console.WriteLine("404 error encountered, skipping file");
							return false;
						}
						else
						{
							throw;
						}
					}
				}
				catch (IOException e)
				{
					Console.WriteLine(e);
					Console.WriteLine();
					Console.WriteLine("Sleeping (30 sec)");
					System.Threading.Thread.Sleep(30000);
				}
			} while (retry);

			// pull out the metadata section
			int metaStartIndex = contents.IndexOf("<script>");
			if (metaStartIndex < 0) throw new UWashException("No metadata found in page");
			metaStartIndex += "<script>".Length;
			string dataText = contents.Substring(metaStartIndex);
			dataText = dataText.Substring(0, dataText.IndexOf("</script>") - 1);
			dataText = dataText.Trim();

			// grab the JSON content
			int leaderLength = "window.__INITIAL_STATE__ = JSON.parse('".Length;
			dataText = dataText.Substring(leaderLength, dataText.Length - (leaderLength + "');".Length));

			// unescape JSON
			dataText = Regex.Unescape(dataText);

			// parse JSON
			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(dataText);

			if (!deser.ContainsKey("item"))
			{
				// that's weird. Try again.
				if (attempts > 2)
				{
					throw new UWashException("Data mysteriously not found.");
				}
				else
				{
					goto retryDownload;
				}
			}

			Dictionary<string, object> item = (Dictionary<string, object>)deser["item"];
			if ((string)item["state"] == "notFound")
			{
				return false;
			}
			item = (Dictionary<string, object>)item["item"];
			object[] fields = (object[])item["fields"];

			foreach (object field in fields)
			{
				Dictionary<string, object> fieldData = (Dictionary<string, object>)field;
				
				//TODO: CleanHtml?
				string value = (string)fieldData["value"];
				value = value.TrimStart('[').TrimEnd(']');

				data[(string)fieldData["key"]] = value;
			}

			return true;
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
			if (data.TryGetValue("lctgm", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("lctgm");
			}
			if (data.TryGetValue("subjec", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("subjec");
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
			if (data.TryGetValue("subjea", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("subjea");
			}

			if (!string.IsNullOrEmpty(lcsh))
			{
				UWashController.data["LCSH"] = lcsh.Replace(lineBreak, "|");
			}
			if (!string.IsNullOrEmpty(lctgm))
			{
				UWashController.data["LCTGM"] = lctgm.Replace(lineBreak, "|");
			}

			return UWashController.data;
		}

		private static bool GetSource(Dictionary<string, string> data, out string result)
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
					string author = ParsePerson(data["Image Source Author"]);
					string[] commasplit = author.Split(',');
					if (commasplit.Length == 2)
					{
						//interpret as last, first
						sb.AppendLine("|last=" + commasplit[0].Trim());
						sb.AppendLine("|first=" + commasplit[1].Trim());
					}
					else
					{
						sb.AppendLine("|author=" + author.Replace(lineBreak, "; "));
					}
				}
				if (data.ContainsKey("Pub. Info."))
				{
					string pubInfo = data["Pub. Info."];
					string[] split1 = pubInfo.Split(colon, 2);
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
		private static string GetAuthor(Dictionary<string, string> data, string lang, out UWashCreator creator)
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
				return GetAuthor(projectConfig.defaultAuthor, "en", out creator);
			}
		}
		
		/// <summary>
		/// Get a string that should be used for the file's 'author' field.
		/// </summary>
		private static string GetAuthor(string name, string lang, out UWashCreator creator)
		{
			//TODO: support multiple creators
			creator = null;

			string finalResult = "";
			foreach (string r in ParseAuthor(name))
			{
				if (string.Equals(r, "Anonymous", StringComparison.InvariantCultureIgnoreCase))
				{
					return "{{anonymous}}";
				}
				//Check for a Creator template
				else if (creatorData.TryGetValue(r, out creator))
				{
					creator.Usage++;
					if (!string.IsNullOrEmpty(creator.Author))
					{
						finalResult += creator.Author;
						continue;
					}
				}
				else
				{
					creatorData.Add(r, new UWashCreator());
				}

				// if we get here, there is not yet a mapping for this creator
				if (projectConfig.allowFailedCreators)
					finalResult += "{{" + lang + "|" + r + "}}";
				else
					throw new UWashException("unrecognized creator|" + r);
			}

			return finalResult;
		}

		private static IEnumerable<string> ParseAuthor(string name)
		{
			string[] authors = name.Split(pipe);
			for (int c = 0; c < authors.Length; c++)
			{
				authors[c] = ParsePerson(authors[c]);

				//try to unswitcheroo Last, First format
				string[] commasplit = authors[c].Split(',');
				if (commasplit.Length == 2)
				{
					string first = commasplit[1].Trim();
					string last = commasplit[0].Trim();
					string suffix = "";

					if (first.EndsWith("Jr.") || first.EndsWith("Sr."))
					{
						suffix = first.Substring(first.Length - 3).Trim();
						first = first.Remove(first.Length - 3).Trim();
					}

					string result = first + " " + last;
					if (!string.IsNullOrEmpty(suffix))
					{
						result += " " + suffix;
					}
					yield return result;
				}
				else
				{
					yield return authors[c];
				}
			}
		}

		private static void ParsePhysicalDescription(string raw, out string medium, out Dimensions dimensions)
		{
			string[] split = raw.Split(';');
			for (int i = 0; i < split.Length; i++)
			{
				split[i] = split[i].Trim();

				// does this look like a dimension?
				if (Dimensions.TryParse(split[i], out dimensions))
				{
					// these are usually backwards
					//TODO: check image aspect ratio
					dimensions = dimensions.Flip();

					// the medium is everything else
					medium = "";
					for (int j = 0; j < split.Length; j++)
					{
						if (j != i)
						{
							medium = StringUtility.Join("; ", medium, split[j]);
						}
					}
					return;
				}
			}

			dimensions = Dimensions.Empty;
			medium = raw;
			return;
		}

		private static string GetDate(Dictionary<string, string> data, out int latestYear)
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

		public static string ParseDate(string date)
		{
			int latestYear;
			return ParseDate(date, out latestYear);
		}

		public static string ParseDate(string date, out int latestYear)
		{
			date = date.Trim('.');

			if (string.IsNullOrEmpty(date))
			{
				latestYear = 9999;
				return "{{unknown|date}}";
			}
			else if (date.EndsWith("~"))
			{
				string yearStr = date.Substring(0, date.Length - 1);
				if (!int.TryParse(yearStr, out latestYear)) latestYear = 9999;
				return "{{other date|ca|" + yearStr + "}}";
			}
			else if (date.StartsWith("ca.", StringComparison.InvariantCultureIgnoreCase))
			{
				int rml = "ca.".Length;
				string yearStr = date.Substring(rml, date.Length - rml).Trim();
				if (!int.TryParse(yearStr, out latestYear)) latestYear = 9999;
				return "{{other date|ca|" + yearStr + "}}";
			}
			else if (date.StartsWith("circa", StringComparison.InvariantCultureIgnoreCase))
			{
				int rml = "circa".Length;
				string yearStr = date.Substring(rml, date.Length - rml).Trim();
				if (!int.TryParse(yearStr, out latestYear)) latestYear = 9999;
				return "{{other date|ca|" + yearStr + "}}";
			}
			else if (date.StartsWith("before"))
			{
				int rml = "before".Length;
				string yearStr = date.Substring(rml, date.Length - rml).Trim();
				if (!int.TryParse(yearStr, out latestYear)) latestYear = 9999;
				return "{{other date|before|" + yearStr + "}}";
			}
			else if (date.StartsWith("voor/before"))
			{
				int rml = "voor/before".Length;
				string yearStr = date.Substring(rml, date.Length - rml).Trim();
				if (!int.TryParse(yearStr, out latestYear)) latestYear = 9999;
				return "{{other date|before|" + yearStr + "}}";
			}
			else
			{
				string[] dashsplit = date.Split('-');
				if (dashsplit.Length == 2 && dashsplit[0].Length == 4
					&& dashsplit[1].Length == 4)
				{
					if (!int.TryParse(dashsplit[1], out latestYear)) latestYear = 9999;
					return "{{other date|between|" + dashsplit[0] + "|" + dashsplit[1] + "}}";
				}
				else
				{
					string[] dateSplit = date.Split(new char[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
					if (dateSplit.Length == 3)
					{
						int year, month, day;
						if (TryParseMonth(dateSplit[0], out month)
							&& int.TryParse(dateSplit[1], out day)
							&& int.TryParse(dateSplit[2], out year))
						{
							latestYear = year;
							return year.ToString() + "-" + month.ToString("00") + "-" + day.ToString("00");
						}
					}
					else if (dateSplit.Length == 2)
					{
						int year, month;
						if (TryParseMonth(dateSplit[0], out month)
							&& int.TryParse(dateSplit[1], out year))
						{
							latestYear = year;
							return year.ToString() + "-" + month.ToString("00");
						}
					}
					else if (dateSplit.Length == 1)
					{
						int year;
						if (int.TryParse(dateSplit[0], out year))
						{
							latestYear = year;
							return year.ToString();
						}
					}

					latestYear = 9999;
					return date;
				}
			}
		}

		private static bool TryParseMonth(string month, out int index)
		{
			switch (month.ToUpper())
			{
				case "JAN":
				case "JAN.":
				case "JANUARY":
					index = 1;
					return true;
				case "FEB":
				case "FEB.":
				case "FEBRUARY":
					index = 2;
					return true;
				case "MAR":
				case "MAR.":
				case "MARCH":
					index = 3;
					return true;
				case "APR":
				case "APR.":
				case "APRIL":
					index = 4;
					return true;
				case "MAY":
				case "MAY.":
					index = 5;
					return true;
				case "JUN":
				case "JUN.":
				case "JUNE":
					index = 6;
					return true;
				case "JUL":
				case "JUL.":
				case "JULY":
					index = 7;
					return true;
				case "AUG":
				case "AUG.":
				case "AUGUST":
					index = 8;
					return true;
				case "SEPT":
				case "SEPT.":
				case "SEP":
				case "SEP.":
				case "SEPTEMBER":
					index = 9;
					return true;
				case "OCT":
				case "OCT.":
				case "OCTOBER":
					index = 10;
					return true;
				case "NOV":
				case "NOV.":
				case "NOVEMBER":
					index = 11;
					return true;
				case "DEC":
				case "DEC.":
				case "DECEMBER":
					index = 12;
					return true;
				default:
					index = 0;
					return false;
			}
		}

		private static string GetDesc(Dictionary<string, string> data)
		{
			string title;
			if (!data.TryGetValue("title", out title))
			{
				throw new UWashException("no title");
			}

			List<string> raw = title.Split(lineBreak, StringSplitOptions.RemoveEmptyEntries).ToList();
			string temp;
			if (data.TryGetValue("descri", out temp))
			{
				raw.AddRange(temp.Split(lineBreak, StringSplitOptions.RemoveEmptyEntries));
			}
			if (data.TryGetValue("histori", out temp))
			{
				raw.AddRange(temp.Split(lineBreak, StringSplitOptions.RemoveEmptyEntries));
			}

			Dictionary<string, string> textByLang = new Dictionary<string,string>();

			//treat first segment as base language
			textByLang[data["Language"]] = raw[0];

			//try to find language for other segments
			for (int c = 1; c < raw.Count; c++)
			{
				string raw2 = raw[c].Trim(parens);
				string newlang = "en";//allowUpload ? DetectLanguage.Detect(raw2) : "en";
				if (textByLang.ContainsKey(newlang))
					textByLang[newlang] += "\n" + raw2;
				else
					textByLang[newlang] = raw2;
			}

			//metadata is always en
			if (data.ContainsKey("Subject"))
			{
				string content = "*Subject: " + data["Subject"].Replace(lineBreak, ", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}
			if (data.ContainsKey("Geographic Subject"))
			{
				string content = "*Geographic Subject: " + data["Geographic Subject"].Replace(lineBreak, ", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}
			if (data.ContainsKey("Category"))
			{
				string content = "*Tag: " + data["Category"].Replace(lineBreak, ", ");
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

		private static char[] dob = new char[] { '0','1','2','3','4','5','6','7','8','9','-',' ','?',',' };
		private static string ParsePerson(string input)
		{
			//Remove trailing DOB/lifetime
			//HACK: be more explicit with regex
			return input.TrimEnd(dob);
		}
		
		public static string TranslateLocationCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";

			if (categoryMap.ContainsKey(input))
			{
				string cached = GetMappedCategory(input);
				if (!string.IsNullOrEmpty(cached) || !config.remapCategories)
				{
					return cached;
				}
			}
			else if (!config.mapCategories)
			{
				return "";
			}

			//simple mapping
			string simpleMap = MapCategory(input, CategoryTranslation.TryFetchCategoryName(Api, input));
			if (!string.IsNullOrEmpty(simpleMap))
			{
				return simpleMap;
			}

			string[] pieces = input.Split(dashdash, StringSplitOptions.RemoveEmptyEntries);

			for (int c = 0; c < pieces.Length; c++)
			{
				pieces[c] = pieces[c].Replace("(state)", "").Trim();
				pieces[c] = pieces[c].Replace("(State)", "").Trim();
			}

			Console.WriteLine("Attempting to map location '" + input + "'.");

			if (pieces.Length >= 2)
			{
				//try flipping last two, with a comma
				string attempt = pieces[pieces.Length - 1].Trim() + ", " + pieces[pieces.Length - 2].Trim();
				string translate = MapCategory(input, CategoryTranslation.TryFetchCategoryName(Api, attempt));
				if (!string.IsNullOrEmpty(translate))
					return translate;
			}

			//check for only the last on wikidata
			string[] entities = WikidataApi.SearchEntities(pieces.Last());
			for (int d = 0; d < Math.Min(5, entities.Length); d++)
			{
				Wikimedia.Entity place = WikidataApi.GetEntity(entities[d]);

				//get country for place
				if (place.HasClaim("P17"))
				{
					IEnumerable<Wikimedia.Entity> parents = place.GetClaimValuesAsEntity("P17", WikidataApi);
					if (place.HasClaim("P131"))
					{
						parents = parents.Concat(place.GetClaimValuesAsEntity("P131", WikidataApi));
					}
					foreach (Wikimedia.Entity parent in parents)
					{
						//look for the parent in the earlier pieces
						for (int c = 0; c < pieces.Length - 1; c++)
						{
							bool countrySuccess = false;
							if (parent.aliases != null && parent.aliases.ContainsKey("en"))
							{
								foreach (string s in parent.aliases["en"])
								{
									if (string.Compare(pieces[c], s, true) == 0)
										countrySuccess = true;
								}
							}
							if (parent.labels != null && parent.labels.ContainsKey("en")
								&& string.Compare(pieces[c], parent.labels["en"], true) == 0)
								countrySuccess = true;

							if (countrySuccess)
							{
								if (place.HasClaim("P373"))
								{
									return MapCategory(input, place.GetClaimValueAsString("P373"));
								}
								else
								{
									//Wikidata has no commons cat :(
									return "";
								}
							}
						}
					}
				}
			}

			return "";
		}

		private static HashSet<string> s_PersonsFailed = new HashSet<string>();
		public static string TranslatePersonCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";

			//TODO: don't recheck on reexecute? See TranslateTagCategory
			string cached = GetMappedCategory(input);
			if (!string.IsNullOrEmpty(cached))
			{
				return cached;
			}

			if (s_PersonsFailed.Contains(input))
			{
				if (projectConfig.allowFailedCreators)
					return input;
				else
					return "";
			}

			//simple mapping
			string simpleMap = MapCategory(input, CategoryTranslation.TryFetchCategoryName(Api, input));
			if (!string.IsNullOrEmpty(simpleMap))
			{
				return simpleMap;
			}
			
			Console.WriteLine("Attempting to map person '" + input + "'.");

			//make sure creator is created
			UWashCreator creator;
			GetAuthor(input, "en", out creator);

			//If they have a creator template, use that
			if (creatorData.ContainsKey(input))
			{
				if (!string.IsNullOrEmpty(creatorData[input].Author))
				{
					string creatorPage = creatorData[input].Author;
					creatorPage = creatorPage.Trim('{').Trim('}');
					if (creatorHomecats.ContainsKey(creatorPage))
					{
						return creatorHomecats[creatorPage];
					}
					else
					{
						//try to find creator template's homecat param
						Wikimedia.Article creatorArticle = Api.GetPage(creatorPage);
						if (creatorArticle != null && !creatorArticle.missing)
						{
							foreach (string s in creatorArticle.revisions[0].text.Split('|'))
							{
								if (s.TrimStart().StartsWith("homecat", StringComparison.InvariantCultureIgnoreCase))
								{
									string homecat = s.Split(equals, 2)[1].Trim();
									if (!string.IsNullOrEmpty(homecat))
									{
										if (!homecat.StartsWith("Category:")) homecat = "Category:" + homecat;
										creatorHomecats[creatorPage] = homecat;
										return homecat;
									}
								}
							}
						}

						//failed to find homecat, use name
						string creatorName = creatorPage;
						if (creatorName.StartsWith("Creator:")) creatorName = creatorName.Substring(8);
						string cat = "Category:" + creatorName;
						creatorHomecats[creatorPage] = "";
						categoryMap[input] = cat;
						return cat;
					}
				}
				else
				{
					return "";
				}
			}

			s_PersonsFailed.Add(input);
			if (projectConfig.allowFailedCreators)
				return input;
			else
				return "";
		}
		
		public static string TranslateTagCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";
			
			if (categoryMap.ContainsKey(input))
			{
				string cached = GetMappedCategory(input);
				if (!string.IsNullOrEmpty(cached) || !config.remapCategories)
				{
					return cached;
				}
			}
			else if (!config.mapCategories)
			{
				return "";
			}
			
			//does it look like it has a parent?
			if (input.Contains('(') && input.EndsWith(")"))
			{
				string switched = "";
				for (int c = input.Length - 1; c >= 0; c--)
				{
					if (input[c] == '(')
					{
						string inParen = input.Substring(c + 1, input.Length - c - 2);
						switched = inParen.Trim() + "--" + input.Substring(0, c).Trim();
						break;
					}
				}
				if (!string.IsNullOrEmpty(switched))
				{
					string mapping = TranslateLocationCategory(switched);
					if (!string.IsNullOrEmpty(mapping))
						return MapCategory(input, mapping);
				}
			}

			//try to create a mapping
			Console.WriteLine("Attempting to map tag '" + input + "'.");
			return MapCategory(input, CategoryTranslation.TranslateCategory(Api, input));
		}

		public static string GetCheckCategoriesTag(int ncats)
		{
			string dmy = "day=" + DateTime.Now.Day + "|month="
				+ CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month)
				+ "|year=" + DateTime.Now.Year;
			if (ncats <= 0)
			{
				return "{{uncategorized|" + dmy + "}}";
			}
			else
			{
				return "{{check categories|" + dmy + "|ncats=" + ncats + "}}";
			}
		}

		/// <summary>
		/// Check if this input is already mapped to a category.
		/// </summary>
		public static string GetMappedCategory(string input)
		{
			if (categoryMap.ContainsKey(input))
			{
				string mappedCat = categoryMap[input];

				//verify that the category is good against the live database
				if (!string.IsNullOrEmpty(mappedCat) && !CategoryTree.AddToTree(mappedCat, 2))
				{
					Console.WriteLine("Failed to find mapped cat '" + input + "'.");
					categoryMap[input] = "";
					return "";
				}

				return mappedCat;
			}
			else
			{
				//flag this category as needing manual mapping
				categoryMap[input] = "";
				return "";
			}
		}

		/// <summary>
		/// Records the category the specified tag has been mapped to.
		/// </summary>
		private static string MapCategory(string tag, string category)
		{
			if (!string.IsNullOrEmpty(category))
			{
				if (!category.StartsWith("Category:")) category = "Category:" + category;
				categoryMap[tag] = category;
				Console.WriteLine("Mapped '" + tag + "' to '" + category + "'.");
				return category;
			}
			else
			{
				// record blank categories so we don't check them again
				if (!categoryMap.ContainsKey(tag))
				{
					categoryMap.Add(tag, category);
				}
				return "";
			}
		}

		private static string CleanHtml(string html)
		{
			int startIndex = -1;
			for (int c = html.Length - 1; c >= 0; c--)
			{
				if (startIndex < 0)
				{
					if (html[c] == '>')
					{
						startIndex = c;
					}
				}
				else
				{
					if (html[c] == '<')
					{
						string tagContent = html.Substring(c + 1, startIndex - c - 1);
						if (!tagContent.Trim().StartsWith("br"))
						{
							html = html.Remove(c, startIndex - c + 1);
						}
						startIndex = -1;
					}
				}
			}

			html = WebUtility.HtmlDecode(html);

			html = html.Trim();

			//TODO trim brs

			return html;
		}
	}

	class UWashException : Exception
	{
		public UWashException(string message)
			: base(message)
		{

		}
	}
}
