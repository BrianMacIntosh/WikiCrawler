using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Globalization;

namespace WikiCrawler
{
	class CreatorData
	{
		public string Key = "";
		public string Recommended = "";
		public string Succeeeded = "";
		public int Usage = 0;
	}

	class UWash
	{
		private const string url = "http://digitalcollections.lib.washington.edu/cdm/singleitem/collection/nowell/id/{0}";
		private const string imageUrl = "http://digitalcollections.lib.washington.edu/utils/ajaxhelper/?CISOROOT=nowell&action=2&CISOPTR={0}&DMSCALE=100&DMWIDTH=99999&DMHEIGHT=99999&DMX=0&DMY=0&DMTEXT=&REC=1&DMTHUMB=0&DMROTATE=0";

		private static Wikimedia.WikiApi Api = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));
		private static Wikimedia.WikiApi WikidataApi = new Wikimedia.WikiApi(new Uri("http://wikidata.org/"));
		private static Wikimedia.CategoryTree CategoryTree = new Wikimedia.CategoryTree(Api);

		private static List<string> knownKeys = new List<string>()
		{
			//used
			"Title",
			"Date",
			"Dates",
			"Notes",
			"Contextual Notes",
			"Photographer",

			"Subjects (LCTGM)",
			"Subjects (LCSH)",
			"Concepts",
			"Location Depicted",
			"Order Number",
			"Physical Description",

			//unused
			"Ordering Information",
			"Ordering Info",
			"Repository",
			"Repository Collection",
			"Object Type",
			"Digital Reproduction Information",
			"Rating",
			"Negative Number",
			"Digital Collection",
			"Photographer's Reference Number",
			"Repository Collection Guide",

			/*"Subject",
			"Image Date",
			"Image Source Author",
			"Subject",
			"Category",
			"Artist/Photographer",
			"Image Source Title",
			"Pub. Info.",
			"Image Source Series",
			"Page No./Plate No.",
			"Geographic Subject",

			//unused
			"Type",
			"Object type",
			"Ordering Information",
			"Copyright",
			"Repository",
			"Digital collection",
			"Rating",

			//other meta
			"Language",*/
		};

		private static WebClient client = new WebClient();
		private static string[] tr = new string[] { "<tr>" };
		private static string[] td = new string[] { "</td>" };
		private static string[] captionSplitters = new string[] { "--", "|" };
		private static char[] space = new char[] { ' ' };
		private static char[] punctuation = { '.', ' ', '\t', '\n', ',', '-' };
		private static char[] pipeOrSemi = { '|', ';' };
		private static char[] pipe = { '|' };
		private static char[] colon = { ':' };
		public static char[] parens = { '(', ')', ' ' };
		private static string[] dashdash = new string[] { "--" };
		private static char[] equals = new char[] { '=' };

		private static Dictionary<string, string> creatorHomecats = new Dictionary<string, string>();

		private static Dictionary<string, string> categoryMap = new Dictionary<string, string>();

		private static Dictionary<string, CreatorData> creatorData = new Dictionary<string, CreatorData>();

		//parsed data for the current document
		private static Dictionary<string, string> data = new Dictionary<string, string>();
		private static bool parseSuccessful = false;

		private const int minIndex = 5;
		private const int maxIndex = 281;

		//configuration
		private const bool remapCategories = false;
		private const bool mapCategories = true;
		private const bool allowUpload = true;
		private const bool allowDataDownload = true;
		private const bool createCreators = false;
		private static int maxFailed = int.MaxValue;
		private static int maxNew = int.MaxValue;
		private static int maxSuccesses = int.MaxValue;
		private static int saveOutInterval = 5;
		private static bool allowFailedCreators = true;

		private static int current = minIndex;
		private static int saveOutCounter = 0;
		private static List<string> failures = new List<string>();

		public static void RebuildFailures()
		{
			//Get all files in the category
			Console.WriteLine("Fetching images in category...");
			List<Wikimedia.Article> articles = new List<Wikimedia.Article>();
			articles.AddRange(Api.GetCategoryPages("Category:Images from the Freshwater and Marine Image Bank"));

			//Sort by name
			Console.WriteLine("Sorting...");
			articles.Sort((a, b) => (a.title.CompareTo(b.title)));

			Console.WriteLine("Finding holes...");
			using (StreamWriter writer = new StreamWriter(new FileStream("uwash_failed.txt", FileMode.Create)))
			{
				while (!articles[0].title.StartsWith("File:FMIB")) articles.RemoveAt(0);
				int currentHas = int.Parse(articles[0].title.Substring(10, 5));
				articles.RemoveAt(0);
				for (int c = minIndex; c <= maxIndex; c++)
				{
					if (c < currentHas)
					{
						writer.WriteLine(c + "|missing from commons");
					}
					else
					{
						while (!articles[0].title.StartsWith("File:FMIB")) articles.RemoveAt(0);
						currentHas = int.Parse(articles[0].title.Substring(10, 5));
						articles.RemoveAt(0);
						Console.WriteLine(currentHas);
					}
				}
			}
		}

		/// <summary>
		/// Removes cached metadata for files that are already uploaded.
		/// </summary>
		public static void CacheCleanup()
		{
			List<string> failures = new List<string>();
			using (StreamReader reader = new StreamReader(new FileStream("uwash_failed.txt", FileMode.Open)))
			{
				while (!reader.EndOfStream)
				{
					failures.Add(reader.ReadLine().Split('|')[0]);
				}
			}

			int count = 0;
			foreach (string s in Directory.GetFiles("uwash_cache"))
			{
				string fileId = Path.GetFileNameWithoutExtension(s);
				if (!failures.Contains(fileId))
				{
					File.Delete(s);
					count++;
				}
			}
			Console.WriteLine("Deleted " + count);
		}

		public static void CreatorCleanup()
		{
			Api.LogIn();

			Console.WriteLine("Reading creators");
			Dictionary<string, CreatorData> creators = new Dictionary<string, CreatorData>();
			using (StreamReader reader = new StreamReader(new FileStream("creator_templates.txt", FileMode.Open), Encoding.Default))
			{
				while (!reader.EndOfStream)
				{
					string[] line = reader.ReadLine().Split('|');
					CreatorData localData = new CreatorData();
					localData.Key = line[0];
					if (localData.Key.StartsWith("Creator:")) localData.Key = localData.Key.Substring(8);
					localData.Usage = 0;
					localData.Recommended = line[2];
					localData.Succeeeded = line[3];
					creators.Add(localData.Key, localData);
				}
			}

			Console.WriteLine("Reading metadata");
			List<Dictionary<string, string>> metadata = new List<Dictionary<string, string>>();
			foreach (string s in Directory.GetFiles("uwash_cache"))
			{
				Dictionary<string, string> thisMeta = new Dictionary<string, string>();
				using (StreamReader reader = new StreamReader(new FileStream(s, FileMode.Open)))
				{
					while (!reader.EndOfStream)
					{
						thisMeta[reader.ReadLine()] = reader.ReadLine();
					}
				}
				metadata.Add(thisMeta);

				// find the creator for this entry
				IEnumerable<string> parsedAuthors = null;
				if (thisMeta.ContainsKey("Artist/Photographer"))
				{
					parsedAuthors = ParseAuthor(thisMeta["Artist/Photographer"]);
				}
				else if (thisMeta.ContainsKey("Image Source Author"))
				{
					parsedAuthors = ParseAuthor(thisMeta["Image Source Author"]);
				}

				if (parsedAuthors != null)
				{
					foreach (string author in parsedAuthors)
					{
						if (creators.ContainsKey(author))
						{
							creators[author].Usage++;
						}
						else
						{
							creators[author] = new CreatorData() { Key = author };
						}
					}
				}
			}

			Console.WriteLine("Processing");
			foreach (CreatorData data in creators.Values)
			{
				Console.WriteLine(data.Key);

				if (data.Recommended.StartsWith("Creator:"))
					data.Recommended = data.Recommended.Substring(8);

				if (!data.Recommended.StartsWith("N/A"))
				{
					//1. validate suggested creator
					if (!string.IsNullOrEmpty(data.Recommended) && string.IsNullOrEmpty(data.Succeeeded))
					{
						Wikimedia.Article creatorArt = Api.GetPage("Creator:" + data.Recommended);
						if (creatorArt != null && !creatorArt.missing)
						{
							data.Succeeeded = creatorArt.title;
						}
					}

					//2. try to create suggested creator
					if (!string.IsNullOrEmpty(data.Recommended) && string.IsNullOrEmpty(data.Succeeeded))
					{
						string trying = data.Recommended;
						Console.WriteLine("Attempting creation...");
						//if (CommonsCreatorFromWikidata.TryMakeCreator(Api, ref trying))
						//	data.Succeeeded = trying;
					}

					//3. give up
					if (!string.IsNullOrEmpty(data.Recommended) && string.IsNullOrEmpty(data.Succeeeded))
					{
						data.Succeeeded = "<FAILED>";
					}
				}
			}

			//Save
			using (StreamWriter writer = new StreamWriter(new FileStream("creator_templates.txt", FileMode.Create), Encoding.Default))
			{
				foreach (CreatorData data in creators.Values.OrderBy(a => a.Usage))
				{
					writer.WriteLine(data.Key + "|" + data.Usage + "|" + data.Recommended + "|" + data.Succeeeded);
				}
			}
		}

		public static void Harvest()
		{
			EasyWeb.SetDelayForDomain(new Uri(imageUrl), 30f);

			//load progress
			if (File.Exists("uwash_progress.txt"))
			{
				using (BinaryReader reader = new BinaryReader(new FileStream("uwash_progress.txt", FileMode.Open)))
				{
					current = reader.ReadInt32();
				}
			}

			//load mapped categories
			if (File.Exists("uwash_cats.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("uwash_cats.txt", FileMode.Open)))
				{
					while (!reader.EndOfStream)
					{
						string[] line = reader.ReadLine().Split('|');
						categoryMap[line[0].ToLower()] = line[1];
					}
				}
			}

			//load known creators
			if (File.Exists("creator_templates.txt"))
			{
				using (StreamReader reader = new StreamReader(new FileStream("creator_templates.txt", FileMode.Open), Encoding.Default))
				{
					while (!reader.EndOfStream)
					{
						string[] line = reader.ReadLine().Split('|');
						CreatorData data = new CreatorData();
						data.Key = line[0];
						data.Recommended = line[2];
						data.Succeeeded = line[3];
						creatorData[data.Key] = data;
					}
				}
			}

			CategoryTree.Load("category_tree.dat");

			if (!Directory.Exists("uwash_images"))
				Directory.CreateDirectory("uwash_images");

			if (!Directory.Exists("uwash_cache"))
				Directory.CreateDirectory("uwash_cache");

			if (!Directory.Exists("uwash_temp"))
				Directory.CreateDirectory("uwash_temp");

			Console.WriteLine("Logging in...");
			Api.LogIn();

			try
			{
				//Try to reprocess old things
				Console.WriteLine();
				Console.WriteLine("Begin reprocessing of previously failed uploads.");
				Console.WriteLine();
				if (File.Exists("uwash_failed.txt"))
				{
					using (StreamReader reader = new StreamReader(new FileStream("uwash_failed.txt", FileMode.Open)))
					{
						while (!reader.EndOfStream)
						{
							string temp = reader.ReadLine();
							if (!string.IsNullOrWhiteSpace(temp)) failures.Add(temp);
						}
					}

					for (int c = 0; c < failures.Count && maxFailed > 0 && maxSuccesses > 0; c++)
					{
						Console.WriteLine();
						data.Clear();
						parseSuccessful = false;

						string[] fail = failures[c].Split('|');
						int failIndex = int.Parse(fail[0]);
						try
						{
							Process(failIndex);

							//If we made it here, we succeeded
							failures.RemoveAt(c);
							c--;
						}
						catch (UWashException e)
						{
							Console.WriteLine("REFAILED:" + e.Message);
							failures[c] = failIndex + "|" + e.Message;
						}

						maxFailed--;

						saveOutCounter++;
						if (saveOutCounter >= saveOutInterval)
						{
							SaveOut();
							saveOutCounter = 0;
						}

						if (File.Exists("STOP")) return;
					}
				}

				//Process new things
				Console.WriteLine();
				Console.WriteLine("Begin processing new files.");
				Console.WriteLine();
				for (; current <= maxIndex && maxNew > 0 && maxSuccesses > 0; )
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
						string fail = current.ToString() + "|";
						if (!parseSuccessful) fail += "PARSE FAIL|";
						fail += e.Message;
						failures.Add(fail);
						Console.WriteLine("ERROR:" + e.Message);
					}
					finally
					{
						//cache the data we had
						if (parseSuccessful)
						{
							using (StreamWriter writer = new StreamWriter(
								new FileStream(GetMetaCacheFilename(current), FileMode.Create), Encoding.Default))
							{
								foreach (KeyValuePair<string, string> kv in data)
								{
									writer.WriteLine(kv.Key);
									writer.WriteLine(kv.Value);
								}
							}
						}
					}

					current++;

					maxNew--;

					saveOutCounter++;
					if (saveOutCounter >= saveOutInterval)
					{
						SaveOut();
						saveOutCounter = 0;
					}

					if (File.Exists("STOP"))
					{
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
			using (BinaryWriter writer = new BinaryWriter(new FileStream("uwash_progress.txt", FileMode.Create)))
			{
				writer.Write(current);
			}

			//write category map
			using (StreamWriter writer = new StreamWriter(new FileStream("uwash_cats.txt", FileMode.Create)))
			{
				foreach (KeyValuePair<string, string> kv in categoryMap)
				{
					writer.WriteLine(kv.Key + "|" + kv.Value);
				}
			}

			//write errors
			using (StreamWriter writer = new StreamWriter(new FileStream("uwash_failed.txt", FileMode.Create)))
			{
				foreach (string s in failures) writer.WriteLine(s);
			}

			//write creators
			using (StreamWriter writer = new StreamWriter(new FileStream("creator_templates.txt", FileMode.Create), Encoding.Default))
			{
				foreach (CreatorData data in creatorData.Values.OrderBy(a => a.Usage))
				{
					writer.WriteLine(data.Key + "|" + data.Usage + "|" + data.Recommended + "|" + data.Succeeeded);
				}
			}

			CategoryTree.Save("category_tree.dat");
		}

		private static void Process(int current)
		{
			Console.WriteLine("BEGIN:" + current);

			//get metadata
			string metacache = GetMetaCacheFilename(current);
			if (File.Exists(metacache))
			{
				//we got data already - load it
				Console.WriteLine("Found cached metadata.");
				using (StreamReader reader = new StreamReader(new FileStream(metacache, FileMode.Open), Encoding.Default))
				{
					while (!reader.EndOfStream)
					{
						string key = reader.ReadLine();
						string value = reader.ReadLine();
						//html decode is for a few messed-up files at start
						data[key] = WebUtility.HtmlDecode(value);
					}
				}
			}
			else
			{
				if (!allowDataDownload)
				{
					throw new UWashException("redownload");
				}

				//retrieve and parse the data from the web
				Console.WriteLine("Downloading metadata.");
				if (!ReadMetadata(current, data))
				{
					return;
				}
			}
			parseSuccessful = true;

			//check for errors that will need inspection
			if (!data.ContainsKey("Title"))
			{
				throw new UWashException("no title");
			}

			//check for new, unknown metadata
			string unused = "";
			foreach (KeyValuePair<string, string> kv in data)
			{
				if (!knownKeys.Contains(kv.Key)) unused += "|" + kv.Key;
			}
			if (!string.IsNullOrEmpty(unused)) throw new UWashException("unused key|" + unused);

			string captionTitle = data["Title"].Split('|')[0].Trim(punctuation).Replace(".", "");
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
			art.title = captionTitle.Replace("/", "") + " (NOWELL " + current + ")";
			art.revisions = new Wikimedia.Revision[1];
			art.revisions[0] = new Wikimedia.Revision();

			string lang = "en";

			List<string> categories = new List<string>();
			string catparse = "";

			string author = GetAuthor(data, lang);

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
			if (data.ContainsKey("Subjects (LCTGM)")) catparse += "|" + data["Subjects (LCTGM)"];
			if (data.ContainsKey("Subjects (LCSH)")) catparse += "|" + data["Subjects (LCSH)"];
			if (data.ContainsKey("Concepts")) catparse += "|" + data["Concepts"];
			/*if (data.ContainsKey("Caption"))
			{
				//max 50
				foreach (string s in data["Caption"].Split(captionSplitters, StringSplitOptions.RemoveEmptyEntries))
				{
					if (s.Length < 50) catparse += "|" + s;
				}
			}*/
			foreach (string s in catparse.Split(pipeOrSemi, StringSplitOptions.RemoveEmptyEntries))
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
			if (data.ContainsKey("Location Depicted")) catparse += "|" + data["Location Depicted"];
			foreach (string s in catparse.Split(pipeOrSemi, StringSplitOptions.RemoveEmptyEntries))
			{
				string cat = TranslateLocationCategory(s.Trim());
				if (!string.IsNullOrEmpty(cat))
				{
					if (cat != "N/A")
					{
						foreach (string catsplit in cat.Split('|'))
						{
							if (string.IsNullOrEmpty(sureLocation)) sureLocation = catsplit;
							if (!categories.Contains(catsplit)) categories.Add(catsplit);
						}
					}
				}
			}

			CategoryTree.RemoveLessSpecific(categories);


			//======== BUILD PAGE TEXT

			StringBuilder content = new StringBuilder();

			content.AppendLine(GetCheckCategoriesTag(categories.Count));

			content.AppendLine("=={{int:filedesc}}==");
			content.AppendLine("{{Photograph");
			content.AppendLine("|photographer=" + author);
			content.AppendLine("|title={{" + lang + "|" + data["Title"] + "}}");
			content.AppendLine("|description=");
			StringBuilder descText = new StringBuilder();

			string notes = "";
			if (data.ContainsKey("Notes"))
			{
				notes += data["Notes"].Replace('|', ' ');
			}
			if (data.ContainsKey("Contextual Notes"))
			{
				if (!string.IsNullOrEmpty(notes))
				{
					notes += "<br/>";
				}
				notes += data["Contextual Notes"].Replace('|', ' ');
			}
			descText.AppendLine(notes);

			if (data.ContainsKey("Subjects (LCTGM)"))
			{
				descText.AppendLine("*Subjects (LCTGM): " + data["Subjects (LCTGM)"].Replace("|", "; "));
			}
			if (data.ContainsKey("Subjects (LCSH)"))
			{
				descText.AppendLine("*Subjects (LCSH): " + data["Subjects (LCSH)"].Replace("|", "; "));
			}
			if (data.ContainsKey("Concepts"))
			{
				descText.AppendLine("*Concepts: " + data["Concepts"].Replace("|", "; "));
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
			else if (data.ContainsKey("Location Depicted"))
			{
				content.AppendLine("|depicted place={{" + lang + "|" + data["Location Depicted"] + "}}");
			}

			content.AppendLine("|date=" + GetDate(data));
			if (data.ContainsKey("Physical Description"))
			{
				content.AppendLine("|medium={{en|" + data["Physical Description"] + "}}");
			}
			content.AppendLine("|institution={{Institution:University of Washington}}");
			content.AppendLine("|source={{UWASH-NOWELL-source}}"); //was department, for Artwork
			content.AppendLine("|accession number={{UWASH-digital-accession|nowell|" + current + "}}");
			if (author == "{{unknown|author}}" || author == "{{anonymous}}")
			{
				content.AppendLine("|permission={{PD-anon-1923}}");
			}
			else
			{
				content.AppendLine("|permission={{PD-old-auto-1923|deathyear=1950}}");
			}
			content.AppendLine("|other_fields={{Information field|name=Order Number|value=" + data["Order Number"] + "}}");
			content.AppendLine("}}");
			content.AppendLine();
			content.AppendLine("[[Category:Images from the Frank H. Nowell Photographs of Alaska Collection to check]]");

			foreach (string s in categories)
			{
				if (!s.StartsWith("Category:"))
					content.AppendLine("[[Category:" + s + "]]");
				else
					content.AppendLine("[[" + s + "]]");
			}


			art.revisions[0].text = content.ToString();

			using (StreamWriter writer = new StreamWriter(new FileStream(Path.Combine("uwash_temp", current + ".txt"), FileMode.Create)))
			{
				writer.Write(content.ToString());
			}

			if (!allowUpload)
			{
				throw new UWashException("not implemented|1");
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
			//string croppath = imagepath;
			string croppath = ImageUtils.AutoCropJpgSolidWhite(imagepath, 0.35f);
			if (!File.Exists(croppath))
			{
				throw new UWashException("crop failed");
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
				maxSuccesses--;
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
			return Path.Combine("uwash_images", index + ".jpg");
		}

		private static string GetMetaCacheFilename(int index)
		{
			return Path.Combine("uwash_cache", index + ".txt");
		}

		private static bool ReadMetadata(int current, Dictionary<string, string> data)
		{
			//Read HTML data
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
							return true;
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

			//Pull out the metadata section
			int metaStartIndex = contents.IndexOf("<!-- META_DATA -->");
			if (metaStartIndex < 0) throw new UWashException("No metadata found in page");
			contents = contents.Substring(metaStartIndex);
			contents = contents.Substring(0, contents.IndexOf("</table>") - 1);

			//Split on table rows
			string[] split = contents.Split(tr, StringSplitOptions.None);

			for (int c = 1; c < split.Length; c++)
			{
				string[] tdsplit = split[c].Split(td, StringSplitOptions.None);

				//split removes dupe spaces
				string key = string.Join(" ", CleanHtml(tdsplit[0]).Split(space, StringSplitOptions.RemoveEmptyEntries));
				string value = CleanHtml(tdsplit[1]);
				value = value.Replace('\n', '|');
				data[key] = value;
			}

			return true;
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
						sb.AppendLine("|author=" + author.Replace("|", "; "));
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
		private static string GetAuthor(Dictionary<string, string> data, string lang)
		{
			if (data.ContainsKey("Photographer"))
			{
				return GetAuthor(data["Photographer"], lang);
			}
			else if (data.ContainsKey("Artist/Photographer"))
			{
				return GetAuthor(data["Artist/Photographer"], lang);
			}
			else if (data.ContainsKey("Image Source Author"))
			{
				return GetAuthor(data["Image Source Author"], lang);
			}
			else
			{
				return "{{unknown|author}}";
			}
		}

		/// <summary>
		/// Get a string that should be used for the file's 'author' field.
		/// </summary>
		private static string GetAuthor(string name, string lang)
		{
			string finalResult = "";
			foreach (string r in ParseAuthor(name))
			{
				//Check for a Creator template
				if (creatorData.ContainsKey(r))
				{
					CreatorData thisCreator = creatorData[r];
					if (thisCreator.Recommended == "N/A")
					{
						finalResult += "{{" + lang + "|" + r + "}}";
						continue;
					}
					else if (thisCreator.Recommended.StartsWith("N/A:"))
					{
						string naName = thisCreator.Recommended.Split(colon, 2)[1];
						if (naName.StartsWith("Category:")) naName = naName.Substring("Category:".Length);
						finalResult += "{{" + lang + "|" + naName + "}}";
						continue;
					}
					else if (thisCreator.Succeeeded == "<FAILED>")
					{
						throw new UWashException("unrecognized creator|" + r);
					}
					else if (!string.IsNullOrEmpty(thisCreator.Succeeeded))
					{
						finalResult += "{{"
							+ (!thisCreator.Succeeeded.StartsWith("Creator:") ? "Creator:" : "")
							+ thisCreator.Succeeeded + "}}";
						continue;
					}
				}

				// if we get here, there is not yet a mapping for this creator
				if (allowFailedCreators)
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

		private static string GetDate(Dictionary<string, string> data)
		{
			string date;
			if (data.ContainsKey("Image Date"))
			{
				date = data["Image Date"];
			}
			else if (data.ContainsKey("Date"))
			{
				date = data["Date"];
			}
			else if (data.ContainsKey("Dates"))
			{
				date = data["Dates"];
			}
			else
			{
				return "{{unknown|date}}";
			}
			return ParseDate(date);
		}

		public static string ParseDate(string date)
		{
			if (string.IsNullOrEmpty(date))
			{
				return "{{unknown|date}}";
			}
			else if (date.EndsWith("~"))
			{
				return "{{other date|ca|" + date.Substring(0, date.Length - 1) + "}}";
			}
			else if (date.StartsWith("ca."))
			{
				int rml = 3;
				return "{{other date|ca|" + date.Substring(rml, date.Length - rml).Trim() + "}}";
			}
			else if (date.StartsWith("before"))
			{
				int rml = "before".Length;
				return "{{other date|before|" + date.Substring(rml, date.Length - rml).Trim() + "}}";
			}
			else if (date.StartsWith("voor/before"))
			{
				int rml = "voor/before".Length;
				return "{{other date|before|" + date.Substring(rml, date.Length - rml).Trim() + "}}";
			}
			else
			{
				string[] dashsplit = date.Split('-');
				if (dashdash.Length == 2 && dashsplit[0].Length == 4
					&& dashsplit[1].Length == 4)
				{
					return "{{other date|between|" + dashsplit[0] + "|" + dashsplit[1] + "}}";
				}

				return date;
			}
		}

		private static string GetDesc(Dictionary<string, string> data)
		{
			string[] raw = (data["Title"] + "|" + data["Notes"]).Split(pipe);
			//TODO: contextual notes

			Dictionary<string, string> textByLang = new Dictionary<string,string>();

			//treat first segment as base language
			textByLang[data["Language"]] = raw[0];

			//try to find language for other segments
			for (int c = 1; c < raw.Length; c++)
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
				string content = "*Subject: " + data["Subject"].Replace("|",", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}
			if (data.ContainsKey("Geographic Subject"))
			{
				string content = "*Geographic Subject: " + data["Geographic Subject"].Replace("|", ", ");
				if (textByLang.ContainsKey("en"))
					textByLang["en"] += "\n" + content;
				else
					textByLang["en"] = content;
			}
			if (data.ContainsKey("Category"))
			{
				string content = "*Tag: " + data["Category"].Replace("|", ", ");
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

		private static HashSet<string> s_LocationsAttempted = new HashSet<string>();
		public static string TranslateLocationCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";

			string cached = GetMappedCategory(input);
			if (!string.IsNullOrEmpty(cached))
			{
				return cached;
			}

			if (s_LocationsAttempted.Contains(input))
				return "";
			else
				s_LocationsAttempted.Add(input);

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

			string cached = GetMappedCategory(input);
			if (!string.IsNullOrEmpty(cached))
			{
				return cached;
			}

			if (s_PersonsFailed.Contains(input))
			{
				if (allowFailedCreators)
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

			string lowerInput = input.ToLower();

			Console.WriteLine("Attempting to map person '" + input + "'.");

			//make sure creator is created
			GetAuthor(input, "en");

			//If they have a creator template, use that
			if (creatorData.ContainsKey(input))
			{
				if (!string.IsNullOrEmpty(creatorData[input].Succeeeded))
				{
					string creatorPage = creatorData[input].Succeeeded;
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
						categoryMap[lowerInput] = cat;
						return cat;
					}
				}
				else if (creatorData[input].Recommended.StartsWith("N/A:Category:"))
				{
					return creatorData[input].Recommended.Substring("N/A:Category:".Length);
				}
				else if (creatorData[input].Recommended.StartsWith("N/A"))
				{
					return "N/A";
				}
			}

			s_PersonsFailed.Add(input);
			if (allowFailedCreators)
				return input;
			else
				return "";
		}

		private static HashSet<string> s_TagsAttempted = new HashSet<string>();
		public static string TranslateTagCategory(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";

			string cached = GetMappedCategory(input);
			if (!string.IsNullOrEmpty(cached))
			{
				return cached;
			}

			if (s_TagsAttempted.Contains(input))
				return "";
			else
				s_TagsAttempted.Add(input);

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
			if (mapCategories)
			{
				Console.WriteLine("Attempting to map tag '" + input + "'.");
				return MapCategory(input, CategoryTranslation.TranslateCategory(Api, input));
			}

			return "";
		}

		public static string GetCheckCategoriesTag(int ncats)
		{
			string dmy = "day = " + DateTime.Now.Day + " | month = "
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
			string lowerInput = input.ToLower();
			if (categoryMap.ContainsKey(lowerInput))
			{
				string mappedCat = categoryMap[lowerInput];

				//verify that the category is good against the live database
				if (!string.IsNullOrEmpty(mappedCat) && !CategoryTree.AddToTree(mappedCat, 2))
				{
					Console.WriteLine("Failed to find mapped cat '" + input + "'.");
					categoryMap[lowerInput] = "";
					return "";
				}

				return mappedCat;
			}
			else
			{
				//flag this category as needing manual mapping
				categoryMap[lowerInput] = "";
				return "";
			}
		}

		/// <summary>
		/// Records a category mapping in the dictionary.
		/// </summary>
		private static string MapCategory(string tag, string category)
		{
			if (!string.IsNullOrEmpty(category))
			{
				if (!category.StartsWith("Category:")) category = "Category:" + category;
				categoryMap[tag.ToLower()] = category;
				Console.WriteLine("Mapped '" + tag + "' to '" + category + "'.");
				return category;
			}
			else
			{
				return "";
			}
		}

		private static string CleanHtml(string html)
		{
			html = html.Replace("<br>", "|");
			html = html.Replace("<br/>", "|");
			html = html.Replace("<br />", "|");
			html = html.Replace("&lt;br&gt;", "|");

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
						html = html.Remove(c, startIndex - c + 1);
						startIndex = -1;
					}
				}
			}

			html = WebUtility.HtmlDecode(html);

			return html.Trim().Trim('|');
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
