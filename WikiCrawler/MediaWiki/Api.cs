using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace MediaWiki
{
    /// <summary>
    /// Contains methods for interacting with the Wikimedia web API.
    /// </summary>
    public class Api
    {
        public static string UserAgent = "BMacZeroBot (brianamacintosh@gmail.com)";

		private readonly Uri Domain;
		private readonly Uri UrlApi;

        private CookieContainer m_cookies;

		private static string s_edittoken;

		public Api(Uri domain)
		{
			Domain = domain;
			UrlApi = new Uri(domain, "w/api.php");
		}

		internal HttpWebRequest CreateApiRequest()
		{
			return CreateApiRequest("");
		}
		
		internal HttpWebRequest CreateApiRequest(string getQuery)
		{
			Uri uri = UrlApi;
			if (!string.IsNullOrEmpty(getQuery))
			{
				uri = new Uri(uri + "?" + getQuery);
			}
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
			request.UserAgent = UserAgent;
			request.CookieContainer = m_cookies;
			return request;
		}

		public bool AutoLogIn()
		{
			WikiCrawler.Credentials credentials = WikiCrawler.Configuration.LoadCredentials();
			return LogIn(credentials.Username, credentials.Password);
		}

		public bool LogIn(string lgname = null, string lgpass = null)
		{
			Console.WriteLine("Logging in to '" + Domain + "':");
			if (string.IsNullOrEmpty(lgname))
			{
				Console.Write("u>");
				lgname = Console.ReadLine();
			}
			if (string.IsNullOrEmpty(lgpass))
			{
				Console.Write("p>");
				lgpass = Console.ReadLine();
			}

            string baseQuery = "format=json"
				+ "&action=login"
                + "&lgname=" + UrlEncode(lgname)
                + "&lgpassword=" + UrlEncode(lgpass);

            //Upload stream
            m_cookies = new CookieContainer();
			HttpWebRequest request = CreateApiRequest();

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

            //Parse and read
            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Dictionary<string, object> login = (Dictionary<string, object>)deser["login"];
            if (((string)login["result"]).Equals("Success"))
            {
                return true;
            }
            else if (((string)login["result"]).Equals("NeedToken"))
            {
                //Send request again, adding lgtoken from "token"
                baseQuery += "&lgtoken=" + UrlEncode((string)login["token"]);
                request = CreateApiRequest();

                //Read response
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
				{
					json = read.ReadToEnd();
				}
            }
            else
            {
                throw new WikimediaException("Logging in returned 'result'='" + ((string)login["result"]) + "'.");
            }

            return false;
        }

		/// <summary>
		/// Combines multiple values for a URL parameter that expects a list of tags, such as 'prop'.
		/// </summary>
		public static string BuildParameterList(params string[] parameters)
		{
			return string.Join("|", parameters);
		}

		/// <summary>
		/// Combines multiple values for a URL parameter that expects a set of namespaces.
		/// </summary>
		public static string BuildNamespaceList(params Namespace[] parameters)
		{
			return string.Join("|", parameters.Select(n => ((int)n).ToString()).ToArray());
		}

		/// <summary>
		/// Returns the url parameter for the specified limit value.
		/// </summary>
		private static string GetLimitParameter(int limit)
		{
			return limit == Limit.Max ? "max" : limit.ToString();
		}

        /// <summary>
        /// Returns the Wiki text of the specified page
        /// </summary>
        public Article GetPage(Article page,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified)
		{
            return GetPage(page.title, prop, iiprop, iilimit, rvprop, rvlimit);
        }

        /// <summary>
        /// Returns the Wiki text of the specified page
        /// </summary>
        public Article GetPage(string title,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified)
		{
            return GetPages(new string[] { title }, prop, iiprop, iilimit, rvprop, rvlimit)[0];
        }

		/// <summary>
		/// Returns the Wiki text for all of the specified pages
		/// </summary>
		public Article[] GetPages(IList<Article> articles,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified)
		{
			return GetPages(
				articles.Select(art => art.title).ToList(),
				prop, iiprop, iilimit, rvprop, rvlimit);
		}

		/// <summary>
		/// Returns the Wiki text for all of the specified pages
		/// </summary>
		public Article[] GetPages(IList<string> titles,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified,
			string clshow = "",
			int cllimit = Limit.Max,
			string cldir = "")
        {
			if (titles.Count == 0) return new Article[0];
			
			//Download stream
			string baseQuery = "format=json"
				+ "&action=query"
				+ "&titles=" + UrlEncode(string.Join("|", titles));
			if (!string.IsNullOrEmpty(prop))
			{
				baseQuery += "&prop=" + UrlEncode(prop);
			}
			if (!string.IsNullOrEmpty(iiprop))
			{
				baseQuery += "&iiprop=" + UrlEncode(iiprop);
			}
			if (iilimit != Limit.Unspecified)
			{
				baseQuery += "&iilimit=" + GetLimitParameter(iilimit);
			}
			if (!string.IsNullOrEmpty(rvprop))
			{
				baseQuery += "&rvprop=" + UrlEncode(rvprop);
			}
			if (rvlimit != Limit.Unspecified)
			{
				baseQuery += "&rvlimit=" + GetLimitParameter(rvlimit);
			}
			if (!string.IsNullOrEmpty(clshow))
			{
				baseQuery += "&clshow=" + UrlEncode(clshow);
			}
			if (cllimit != Limit.Unspecified)
			{
				baseQuery += "&cllimit=" + GetLimitParameter(cllimit);
			}
			if (!string.IsNullOrEmpty(cldir))
			{
				baseQuery += "&cldir=" + UrlEncode(cldir);
			}

			HttpWebRequest request = CreateApiRequest();

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

            //Parse and read
            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Dictionary<string, object> query = (Dictionary<string, object>)deser["query"];
            Dictionary<string, object> pages = (Dictionary<string, object>)query["pages"];
            Article[] ret = new Article[pages.Count];
            int current = 0;
            foreach (KeyValuePair<string, object> page in pages)
            {
				Dictionary<string, object> jsonData = (Dictionary<string, object>)page.Value;
				if (jsonData.ContainsKey("invalid"))
				{
					continue;
				}
				ret[current++] = new Article(jsonData);
            }

            return ret;
        }

		/// <summary>
		/// Creates a new page with the specified content.
		/// </summary>
		public bool CreatePage(Article newpage,
			string summary,
			string tags = "",
			bool minor = false,
			bool notminor = false,
			bool bot = true)
		{
			return SetPage(newpage, summary, tags, minor, notminor, bot, createonly: true);
		}

		/// <summary>
		/// Edit the specified existing page to have the specified content.
		/// </summary>
		public bool EditPage(Article newpage,
			string summary,
			string tags = "",
			bool minor = false,
			bool notminor = false,
			bool bot = true)
		{
			return SetPage(newpage, summary, tags, minor, notminor, bot, nocreate: true);
		}

		/// <summary>
		/// Set the content of the specified page.
		/// </summary>
		public bool SetPage(Article newpage,
			string summary,
			string tags = "",
			bool minor = false,
			bool notminor = false,
			bool bot = true,
			bool createonly = false,
			bool nocreate = false)
        {
            MD5 hashFn = MD5.Create();
            byte[] hash = hashFn.ComputeHash(Encoding.UTF8.GetBytes(newpage.revisions[0].text.ToCharArray()));
            string md5 = Convert.ToBase64String(hash);

			if (string.IsNullOrEmpty(newpage.edittoken))
				newpage.edittoken = GetCsrfToken();

            string baseQuery = "format=json"
                + "&action=edit"
                + "&title=" + UrlEncode(newpage.title)
                + "&text=" + UrlEncode(newpage.revisions[0].text)
                + "&summary=" + UrlEncode(summary)
                //+ "&md5=" + UrlEncode(md5)
                + "&starttimestamp=" + UrlEncode(newpage.starttimestamp)
                + "&token=" + UrlEncode(newpage.edittoken)
				+ "&assert=bot";
			if (!string.IsNullOrEmpty(tags))
			{
				baseQuery += "&tags=" + UrlEncode(tags);
			}
			if (bot)
			{
				baseQuery += "&bot";
			}
			if (minor)
			{
				baseQuery += "&minor";
			}
			if (notminor)
			{
				baseQuery += "&notminor";
			}
			if (createonly)
			{
				baseQuery += "&createonly";
			}
			if (nocreate)
			{
				baseQuery += "&nocreate";
			}

            HttpWebRequest request = CreateApiRequest();

            //Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            if (deser.ContainsKey("error"))
            {
                Dictionary<string, object> error = (Dictionary<string, object>)deser["error"];
				throw new WikimediaException(error["code"] + ": " + error["info"]);
			}

			newpage.Dirty = false;
			newpage.Changes.Clear();
            return true;
        }

		/// <summary>
		/// Undoes the specified revision.
		/// </summary>
		public bool UndoRevision(int pageid, int revisionid, bool bot = true)
		{
			if (string.IsNullOrEmpty(s_edittoken))
				s_edittoken = GetCsrfToken();

			string baseQuery = "format=json"
				+ "&action=edit"
				+ "&pageid=" + pageid.ToString()
				+ "&undo=" + revisionid.ToString()
				+ "&nocreate"
				+ "&token=" + UrlEncode(s_edittoken);
			if (bot)
			{
				baseQuery += "&bot";
			}

			HttpWebRequest request = CreateApiRequest();

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
			if (deser.ContainsKey("error"))
			{
				Dictionary<string, object> error = (Dictionary<string, object>)deser["error"];
				throw new WikimediaException(error["code"] + ": " + error["info"]);
			}

			return true;
		}

		/// <summary>
		/// Purges the cache for the specified pages.
		/// </summary>
		public bool PurgePages(IList<Article> inpages)
		{
			List<string> pagenames = inpages.Select(page => page.title).ToList();
			return PurgePages(pagenames);
		}

		/// <summary>
		/// Purges the cache for the specified pages.
		/// </summary>
		public bool PurgePages(IList<string> inpages)
		{
			if (inpages.Count == 0) return true;
			
			//Download stream
			string baseQuery = "format=json"
				+ "&action=purge"
				+ "&titles=" + UrlEncode(string.Join("|", inpages))
				+ "&forcelinkupdate";
			HttpWebRequest request = CreateApiRequest();

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);

			return true;
		}

		/// <summary>
		/// Searches for pages by a search query.
		/// </summary>
		public IEnumerable<Article> Search(string srsearch, string srnamespace = "", int srlimit = Limit.Max)
		{
			//TODO: test me

			//Download stream
			string baseQuery = "format=json"
				+ "&action=query"
				+ "&list=search"
				+ "&srsearch=" + UrlEncode(srsearch);
			if (!string.IsNullOrEmpty(srnamespace))
			{
				baseQuery += "&srnamespace=" + UrlEncode(srnamespace);
			}
			if (srlimit != Limit.Unspecified)
			{
				baseQuery += "&srlimit=" + GetLimitParameter(srlimit);
			}
			string query = baseQuery + "&continue=";

			bool doContinue = false;

			do
			{
				HttpWebRequest request = CreateApiRequest();

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, query)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)deser["query"])["search"])
				{
					yield return new Article(page);
				}

				doContinue = deser.ContainsKey("continue");
				if (doContinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					query = baseQuery;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						query += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (doContinue);
		}

		/// <summary>
		/// Searches for entities with the specified title.
		/// </summary>
		public string[] SearchEntities(string search, string language = "en")
		{
			string baseQuery = "format=json"
				+ "&action=wbsearchentities"
				+ "&type=item"
				+ "&search=" + UrlEncode(search);
			if (!string.IsNullOrEmpty(language))
			{
				baseQuery += "&language=" + UrlEncode(language);
			}
			
			HttpWebRequest request = CreateApiRequest(baseQuery);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
			object[] searchJson = (object[])deser["search"];
			string[] results = new string[searchJson.Length];
			for (int c = 0; c < searchJson.Length; c++)
			{
				results[c] = (string)((Dictionary<string, object>)searchJson[c])["id"];
			}

			return results;
		}

		public bool CreateEntityClaim(Entity entity, string property, string value, string summary, bool bot = true)
		{
			string baseQuery = "format=json"
				+ "&action=wbcreateclaim"
				+ "&entity=" + entity.id
				+ "&snaktype=value"
				+ "&property=" + UrlEncode(property)
				+ "&value=\"" + UrlEncode(value) + "\""
				+ "&summary=" + UrlEncode(summary)
				+ "&token=" + UrlEncode(GetCsrfToken())
				+ "&assert=bot";
			if (bot)
			{
				baseQuery += "&bot";
			}

			HttpWebRequest request = CreateApiRequest(baseQuery);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
			if (deser.ContainsKey("error"))
			{
				Dictionary<string, object> error = (Dictionary<string, object>)deser["error"];
				throw new WikimediaException(error["code"] + ": " + error["info"]);
			}
			return true;
		}

		/// <summary>
		/// Returns the Wiki entity data of the specified page
		/// </summary>
		public Entity GetEntity(Article page)
		{
			return GetEntity(page.title);
		}

		/// <summary>
		/// Returns the Wiki entity data of the specified page
		/// </summary>
		public Entity GetEntity(string page)
		{
			Entity[] entities = GetEntities(new string[] { page });
			if (entities != null && entities.Length > 0)
			{
				return entities[0];
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Returns the Wiki entity data for all of the specified pages
		/// </summary>
		public Entity[] GetEntities(IList<string> ids)
		{
			//TODO: continue support?

			if (ids.Count == 0) return new Entity[0];
			
			string baseQuery = "format=json"
				+ "&action=wbgetentities"
				+ "&ids=" + UrlEncode(string.Join("|", ids));
			HttpWebRequest request = CreateApiRequest(baseQuery);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
			if (!deser.ContainsKey("entities"))
			{
				return new Entity[0];
			}
			Dictionary<string, object> entities = (Dictionary<string, object>)deser["entities"];

			Entity[] ret = new Entity[entities.Count];
			int current = 0;
			foreach (KeyValuePair<string, object> page in entities)
			{
				ret[current++] = new Entity((Dictionary<string, object>)page.Value);
			}

			return ret;
		}

		/// <summary>
		/// Returns contributions for the specified user.
		/// </summary>
		/// <param name="ucuser">The name of the user (exclude 'User:')</param>
		public IEnumerable<Contribution> GetContributions(string ucuser, string ucstart, string ucend, int uclimit = Limit.Max)
		{
			//Encode page names
			ucuser = UrlEncode(ucuser);

			string baseQuery = "format=json" +
				"&action=query" +
				"&list=usercontribs" +
				"&ucuser=" + UrlEncode(ucuser) +
				"&ucstart=" + UrlEncode(ucstart) +
				"&ucend=" + UrlEncode(ucend);
			if (uclimit != Limit.Unspecified)
			{
				baseQuery += "&uclimit=" + GetLimitParameter(uclimit);
			}
			string query = baseQuery + "&continue=";

			bool doContinue = false;

			do
			{
				HttpWebRequest request = CreateApiRequest();

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, query)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)deser["query"])["usercontribs"])
				{
					yield return new Contribution(page);
				}

				doContinue = deser.ContainsKey("continue");
				if (doContinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					query = baseQuery;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						query += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (doContinue);
		}

		public bool UploadFromWeb(Article newpage, string url, string summary, bool bot = true)
		{
			string baseQuery = "format=json"
                + "&action=upload"
                + "&filename=" + UrlEncode(newpage.title)
                + "&summary=" + UrlEncode(summary)
				+ "&url=" + UrlEncode(url)
                + "&starttimestamp=" + UrlEncode(newpage.starttimestamp)
                + "&token=" + UrlEncode(GetCsrfToken())
				+ "&ignorewarnings"
				+ "&assert=bot";
			if (bot)
			{
				baseQuery += "&bot";
			}

			if (newpage.revisions != null && newpage.revisions.Length > 0)
			{
				baseQuery += "&text=" + UrlEncode(newpage.revisions[0].text);
			}

			HttpWebRequest request = CreateApiRequest();

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
			if (deser.ContainsKey("error"))
			{
				Dictionary<string, object> error = (Dictionary<string, object>)deser["error"];
				throw new WikimediaException(error["code"] + ": " + error["info"]);
			}
			return true;
		}

        /// <summary>
        /// Uploads media from the local computer.
        /// </summary>
        /// <returns>Success</returns>
        public bool UploadFromLocal(Article newpage, string path, string summary, bool bot = true)
        {
			if (!newpage.title.StartsWith("File:"))
			{
				newpage.title = "File:" + newpage.title;
			}

            //Download stream
            Dictionary<string, string> data = new Dictionary<string, string>();
            data["format"] = "json";
            data["action"] = "upload";
            data["filename"] = newpage.title;
            data["token"] = GetCsrfToken();
            data["ignorewarnings"] = "1";
			data["summary"] = summary;
			data["comment"] = summary;
			if (bot) data["bot"] = "1";

            if (newpage.revisions != null && newpage.revisions.Length > 0)
            {
                data["text"] = newpage.revisions[0].text;
            }

            HttpWebRequest request = CreateApiRequest();

            string filetype = MimeUtility.GetMimeFromExtension(Path.GetExtension(path));

			byte[] rawfile;
			using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open)))
			{
				rawfile = reader.ReadBytes((int)reader.BaseStream.Length);
			}

			if (rawfile.Length > 104857600)
			{
				throw new Exception("Chunked upload required");
			}

			object[] dupes = GetDuplicateFiles(rawfile);
			if (dupes != null && dupes.Length > 0)
			{
				throw new DuplicateFileException(newpage.title, dupes);
			}

		reupload:
			string json;
			try
			{
				//Read response
				using (FileStream filestream = new FileStream(path, FileMode.Open))
				{
					using (StreamReader read = new StreamReader(EasyWeb.Upload(request, data, newpage.title, filetype, filestream)))
					{
						json = read.ReadToEnd();
					}
				}
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
					throw e;
				}
			}

            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            if (deser.ContainsKey("error"))
            {
				Dictionary<string, object> error = (Dictionary<string, object>)deser["error"];
				if ((string)error["code"] == "verification-error")
				{
					object[] details = (object[])error["details"];
					if ((string)details[0] == "filetype-mime-mismatch")
					{
						// attempt to automatically fix extension/mime mismatch
						string mime = (string)details[2];
						string actualExt = MimeUtility.GetExtensionFromMime(mime);
						newpage.title = Path.ChangeExtension(newpage.title, actualExt);
						return UploadFromLocal(newpage, path, summary, bot);
					}
				}
				
				throw new WikimediaException(error["code"] + ": " + error["info"]);
			}
            return true;
        }

		//TODO: unnecessary?
        public static Dictionary<string, object> GetCommonMetadata(Dictionary<string, object> file)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            if (file.ContainsKey("imageinfo"))
            {
                object[] ii = (object[])file["imageinfo"];
                for (int c = 0; c < ii.Length; c++)
                {
                    Dictionary<string, object> cmCandidate = (Dictionary<string, object>)ii[c];
                    if (cmCandidate.ContainsKey("commonmetadata"))
                    {
                        object[] cm = (object[])cmCandidate["commonmetadata"];
                        foreach (Dictionary<string, object> dict in cm)
                            metadata[(string)dict["name"]] = dict["value"];
                        break;
                    }
                }
            }
            return metadata;
        }

		//TODO: unnecessary?
		public static Dictionary<string, object> GetMetadata(Dictionary<string, object> file)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            if (file.ContainsKey("imageinfo"))
            {
                object[] ii = (object[])file["imageinfo"];
                for (int c = 0; c < ii.Length; c++)
                {
                    Dictionary<string, object> cmCandidate = (Dictionary<string, object>)ii[c];
                    if (cmCandidate.ContainsKey("metadata"))
                    {
                        object[] cm = (object[])cmCandidate["metadata"];
                        foreach (Dictionary<string, object> dict in cm)
                            metadata[(string)dict["name"]] = dict["value"];
                        break;
                    }
                }
            }
            return metadata;
        }

		/// <summary>
		/// Checks for already-existing duplicate copies of the file.
		/// </summary>
		/// <returns>An array of key-value dictionaries describing the duplicate files(s)</returns>
        public object[] GetDuplicateFiles(byte[] file, string prop = Prop.imageinfo)
        {
            SHA1 algo = SHA1.Create();
            byte[] shaData = algo.ComputeHash(file);
            StringBuilder shaHex = new StringBuilder();
            for (int i = 0; i < shaData.Length; i++)
                shaHex.Append(shaData[i].ToString("x2"));

			//Download stream
			string baseQuery = "format=json"
				+ "&action=query"
				+ "&list=allimages"
				+ "&aisha1=" + shaHex;
			if (!string.IsNullOrEmpty(prop))
			{
				baseQuery += "&prop=" + prop;
			}
			
            HttpWebRequest request = CreateApiRequest(baseQuery);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

            //Parse and read
            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Dictionary<string, object> query = (Dictionary<string, object>)deser["query"];
            if (query.ContainsKey("allimages"))
                return (object[])query["allimages"];
            else
                return null;
        }

        private string GetCsrfToken()
        {
			string baseQuery = "format=json&action=query&meta=tokens&type=csrf";
            HttpWebRequest request = CreateApiRequest(baseQuery);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

            //Parse and read
            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);

            return (string)((Dictionary<string, object>)((Dictionary<string, object>)deser["query"])["tokens"])["csrftoken"];
        }
		
		/// <summary>
		/// Returns all pages in the specified category and its descendents (depth-first).
		/// </summary>
		public IEnumerable<Article> GetCategoryPagesRecursive(string category, int maxDepth = int.MaxValue)
		{
			return GetCategoryPagesRecursive(category, maxDepth, new HashSet<string>());
		}

		private IEnumerable<Article> GetCategoryPagesRecursive(string category, int maxDepth, HashSet<string> alreadyHandledSubcats)
		{
			if (maxDepth <= 0) yield break;
			alreadyHandledSubcats.Add(category);
			foreach (Article article in GetCategoryEntries(category, cmtype: BuildParameterList(CMType.page, CMType.subcat)))
			{
				if (article.ns == Namespace.Category)
				{
					if (!alreadyHandledSubcats.Contains(article.title))
					{
						foreach (Article art2 in GetCategoryPagesRecursive(article.title, maxDepth - 1, alreadyHandledSubcats))
						{
							yield return art2;
						}
					}
				}
				else
				{
					yield return article;
				}
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		public IEnumerable<Article> GetCategoryEntries(
			string cmtitle,
			string cmtype = "",
			string cmnamespace = "",
			int cmlimit = Limit.Max,
			string cmstartsortkeyprefix = "")
		{
			string baseQuery = "format=json"
				+ "&action=query"
				+ "&list=categorymembers"
				+ "&cmtitle=" + UrlEncode(cmtitle);
			if (!string.IsNullOrEmpty(cmtype))
			{
				baseQuery += "&cmtype=" + UrlEncode(cmtype);
			}
			if (!string.IsNullOrEmpty(cmnamespace))
			{
				baseQuery += "&cmnamespace=" + UrlEncode(cmnamespace);
			}
			if (cmlimit != Limit.Unspecified)
			{
				baseQuery += "&cmlimit=" + GetLimitParameter(cmlimit);
			}
			if (!string.IsNullOrEmpty(cmstartsortkeyprefix))
			{
				baseQuery += "&cmstartsortkeyprefix=" + UrlEncode(cmstartsortkeyprefix);
			}
			string query = baseQuery + "&continue=";

			bool doContinue = false;

			do
			{
				HttpWebRequest request = CreateApiRequest();

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, query)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)deser["query"])["categorymembers"])
				{
					yield return new Article(page);
				}

				doContinue = deser.ContainsKey("continue");
				if (doContinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					query = baseQuery;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						query += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (doContinue);
		}
		
        internal static string UrlEncode(string str)
        {
            return System.Web.HttpUtility.UrlEncode(str);
        }
    }
}
