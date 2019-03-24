using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Wikimedia
{
    /// <summary>
    /// Contains helper methods for manipulating wikitext.
    /// </summary>
    static class WikiUtils
    {
        /// <summary>
        /// Removes the specified category from the text if it exists.
        /// </summary>
        public static string RemoveCategory(string name, string text)
        {
			//TODO: whitespace on ends is legal
            string cat = "[[" + name + "]]";
            return text.Replace(cat + "\n", "").Replace(cat, "");
        }

		public static string RemoveDuplicateCategories(string text)
		{
			HashSet<string> alreadySeen = new HashSet<string>();
			foreach (string s in GetCategories(text))
			{
				if (alreadySeen.Contains(s))
				{
					//TODO: preserve sortkeys
					text = RemoveCategory(s, text);
					text = AddCategory(s, text);
				}
				else
				{
					alreadySeen.Add(s);
				}
			}
			return text;
		}

		/// <summary>
		/// Returns true if the specified category exists in the text.
		/// </summary>
		public static bool HasCategory(string name, string text)
		{
			if (name.Length <= 0) throw new ArgumentException("Category name cannot be empty.", "name");

			if (name.StartsWith("Category:") || name.StartsWith("category:"))
			{
				name = name.Substring("Category:".Length);
			}

			foreach (string cat in GetCategories(text))
			{
				string catName = cat.Substring("Category:".Length);
				if (char.ToLower(catName[0]) == char.ToLower(name[0])
					&& catName.Substring(1) == name.Substring(1))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Add the specified category to the text.
		/// </summary>
		public static string AddCategory(string name, string text)
		{
			if (!name.StartsWith("Category:") && !name.StartsWith("category:"))
			{
				name = "Category:" + name;
			}
			if (!HasCategory(name, text))
			{
				int c = text.Length - 1;
				int lastCatStart = text.LastIndexOf("[[Category:", StringComparison.OrdinalIgnoreCase);
				if (lastCatStart >= 0)
				{
					// if there are existing categories, add after the last one
					c = text.IndexOf("]]", lastCatStart) + 1;
				}
				else
				{
					// backtrack past any interwiki links, and comments
					for (; c >= 0; c--)
					{
						if (!char.IsWhiteSpace(text[c]))
						{
							if (c > 0 && text[c] == ']' && text[c - 1] == ']')
							{
								int linkstart = text.Substring(0, c).LastIndexOf("[[");
								if (linkstart >= 0)
								{
									string content = text.Substring(linkstart + 2, c - 2 - (linkstart + 2));
									string[] contentNsSplit = content.Split(':');
									if (contentNsSplit.Length >= 2
										&& string.Compare(contentNsSplit[0], "category", true) != 0
										&& string.Compare(contentNsSplit[0], "template", true) != 0)
									{
										// interwiki, keep going
										c = linkstart;
									}
									else
									{
										// not an interwiki, break
										break;
									}
								}
								else
								{
									break;
								}
							}
							else if (c > 1 && text[c] == '>' && text[c - 1] == '-' && text[c - 2] == '-')
							{
								// backtrack through comment
								c = text.Substring(0, c).LastIndexOf("<!--");
							}
							else
							{
								break;
							}
						}
					}
				}

				return text.Substring(0, c + 1)
					+ "\n[[" + name + "]]"
					+ text.Substring(c + 1, text.Length - (c + 1));
			}
			else
			{
				return text;
			}
		}

		/// <summary>
		/// Returns true if the text contains any categories.
		/// </summary>
		public static bool HasNoCategories(string text)
		{
			return !GetCategories(text).Any();
		}

		/// <summary>
		/// Returns an array of all the directly-referenced parent categories of this article.
		/// </summary>
		public static IEnumerable<string> GetCategories(string text)
		{
			//TODO: whitespace after [[ is legal
			string[] catOpen = new string[] { "[[Category:", "[[category:" };
			string[] catClose = new string[] { "]]" };
			string[] sp1 = text.Split(catOpen, StringSplitOptions.None);
			for (int c = 1; c < sp1.Length; c++)
			{
				yield return "Category:" + sp1[c].Split(catClose, StringSplitOptions.None)[0].Split('|')[0].Trim();
			}
		}

		/// <summary>
		/// Remove HTML comments from the text.
		/// </summary>
		public static string RemoveComments(string text, string prefix = "<!--")
		{
			Marker start = new Marker(prefix);
			Marker end = new Marker("-->");
			int startPos = -1;
			for (int c = 0; c < text.Length; c++)
			{
				if (startPos < 0)
				{
					if (start.MatchAgainst(text[c]))
					{
						startPos = c - start.Length + 1;
					}
				}
				else
				{
					if (end.MatchAgainst(text[c]))
					{
						text = text.Substring(0, startPos) + text.Substring(c + 1);
						c = startPos - 1;
						startPos = -1;
					}
				}
			}
			return text;
		}

		public static string GetTemplateParameter(string param, string text)
		{
			int eat;
			return GetTemplateParameter(param, text, out eat);
		}

		public static string GetTemplateParameter(string param, string text, out int paramValueLocation)
		{
			int state = 0;
			Marker paramName = new Marker(param, true);
			paramValueLocation = 0;
			for (int c = 0; c < text.Length; c++)
			{
				if (state == 1)
				{
					//parameter name
					if (paramName.MatchAgainst(text[c]))
					{
						state = 2;
						continue;
					}
				}
				if (state == 2)
				{
					//equals
					if (text[c] == '=')
					{
						state = 3;
						continue;
					}
				}
				if (state == 3)
				{
					//eat whitespace
					if (!char.IsWhiteSpace(text[c]))
					{
						state = 4;
						paramValueLocation = c;
					}
				}
				if (state == 4)
				{
					//read param content
					bool templateEnd = c < text.Length - 1 && text[c] == '}' && text[c + 1] == '}';
					if (text[c] == '|' || templateEnd)
					{
						return text.Substring(paramValueLocation, c - paramValueLocation).Trim();
					}
				}

				//pipe resets any time
				if (text[c] == '|')
				{
					state = 1;
					paramName.Reset();
					continue;
				}
			}
			return "";
		}

		/// <summary>
		/// Removes the first occurence of the specified template from the text.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string RemoveTemplate(string templateName, string text, out string template)
		{
			//TOOD: support nested templates
			string startMarker = "{{" + templateName;
			int templateStart = text.IndexOf(startMarker);
			if (templateStart >= 0)
			{
				int templateEnd = text.IndexOf("}}", templateStart) + 2;

				// if the next character is a line return, get that too
				if (templateEnd < text.Length && text[templateEnd] == '\n')
				{
					templateEnd++;
				}

				template = text.Substring(templateStart, templateEnd - templateStart);
				return text.Substring(0, templateStart) + text.Substring(templateEnd, text.Length - templateEnd);
			}
			else
			{
				template = "";
				return text;
			}
		}

		/// <summary>
		/// Adds the specified interwiki link to the page if it doesn't already exist.
		/// </summary>
		/// <returns>The new text.</returns>
		public static string AddInterwiki(string wiki, string page, string text)
		{
			string link = "[[" + wiki + ":" + page + "]]";
			if (!text.Contains(link))
			{
				return text + "\n" + link;
			}
			else
			{
				return text;
			}
		}

		/// <summary>
		/// Get the sortkey of the fetched article.
		/// </summary>
		public static string GetSortkey(Wikimedia.Article article)
		{
			//TODO: support template transclusions
			if (!Article.IsNullOrEmpty(article))
			{
				string key = "{{DEFAULTSORT:";
				int defaultSort = article.revisions[0].text.IndexOf(key);
				if (defaultSort >= 0)
				{
					int defaultEnd = article.revisions[0].text.IndexOf("}}", defaultSort);
					return article.revisions[0].text.Substring(defaultSort, defaultEnd - defaultSort - key.Length - 1);
				}
				else
				{
					return article.GetTitle();
				}
			}
			else
			{
				return "";
			}
		}
    }

    /// <summary>
    /// Contains methods for interacting with the Wikimedia web API.
    /// </summary>
    public class WikiApi
    {
        public static string UserAgent = "BMacZeroBot (brianamacintosh@gmail.com)";

		private readonly Uri Domain;
		private readonly Uri UrlApi;

        private CookieContainer cookies;

		private static string edittoken;

		public WikiApi(Uri domain)
		{
			Domain = domain;
			Uri.TryCreate(domain, "w/api.php", out UrlApi);
		}

		internal HttpWebRequest CreateWebRequest()
		{
			return CreateWebRequest(UrlApi);
		}

		internal HttpWebRequest CreateWebRequest(Uri uri)
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
			request.UserAgent = UserAgent;
			request.CookieContainer = cookies;
			return request;
		}

		public bool LogIn(string user = null, string pass = null)
		{
			Console.WriteLine("Logging in to '" + Domain + "':");
			if (string.IsNullOrEmpty(user))
			{
				Console.Write("u>");
				user = Console.ReadLine();
			}
			if (string.IsNullOrEmpty(pass))
			{
				Console.Write("p>");
				pass = Console.ReadLine();
			}

            string data =
				"action=login" +
                "&format=json" +
                "&lgname=" + UrlEncode(user) +
                "&lgpassword=" + UrlEncode(pass);

            //Upload stream
            cookies = new CookieContainer();
			HttpWebRequest request = CreateWebRequest(UrlApi);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
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
                data += "&lgtoken=" + UrlEncode((string)login["token"]);
                request = CreateWebRequest(UrlApi);

                //Read response
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
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
        /// Returns the Wiki text of the specified page
        /// </summary>
        public Article GetPage(Article page, string prop = "info|revisions")
        {
            return GetPage(page.title, prop);
        }

        /// <summary>
        /// Returns the Wiki text of the specified page
        /// </summary>
        public Article GetPage(string page, string prop = "info|revisions")
        {
            return GetPages(new string[] { page }, prop)[0];
        }

        /// <summary>
        /// Returns the Wiki text for all of the specified pages
        /// </summary>
        public Article[] GetPages(IList<string> inpages, string prop = "info|revisions")
        {
			string[] props = prop.Split('|');
			if (inpages.Count == 0) return new Article[0];

            //Encode page names
            for (int c = 0; c < inpages.Count; c++) inpages[c] = UrlEncode(inpages[c]);

			//Download stream
			string parameters = "?format=json" +
				"&action=query" +
				"&titles=" + string.Join("|", inpages) +
				"&prop=" + prop;
			if (props.Contains("revisions"))
			{
				parameters += "&rvprop=content";
			}
			Uri url = new Uri(UrlApi + parameters);
            HttpWebRequest request = CreateWebRequest(url);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
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
        /// Edit the specified page to have the specified text
        /// </summary>
        public bool SetPage(Article newpage, string summary, bool minor, bool bot, bool nocreate = true)
        {
            MD5 hashFn = MD5.Create();
            byte[] hash = hashFn.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newpage.revisions[0].text.ToCharArray()));
            string md5 = Convert.ToBase64String(hash);

			if (string.IsNullOrEmpty(newpage.edittoken))
				newpage.edittoken = GetCsrfToken();

            string data =
                "action=edit" +
                "&title=" + UrlEncode(newpage.title) +
                "&format=json" +
                "&text=" + UrlEncode(newpage.revisions[0].text) +
                "&summary=" + UrlEncode(summary) +
                (bot ? "&bot=1" : "") +
                (minor ? "&minor=1" : "") +
                //"&md5=" + UrlEncode(md5) +
                "&starttimestamp=" + UrlEncode(newpage.starttimestamp) +
                (nocreate ? "&nocreate=1" : "") +
                "&token=" + UrlEncode(newpage.edittoken) +
				"&assert=bot";

            HttpWebRequest request = CreateWebRequest(UrlApi);

            //Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
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
		public bool UndoRevision(int pageid, int revisionid, bool bot)
		{
			if (string.IsNullOrEmpty(edittoken))
				edittoken = GetCsrfToken();

			string data =
				"action=edit" +
				"&pageid=" + pageid.ToString() +
				"&undo=" + revisionid.ToString() +
				"&format=json" +
				(bot ? "&bot=1" : "") +
				"&nocreate=1" +
				"&token=" + UrlEncode(edittoken);

			HttpWebRequest request = CreateWebRequest(UrlApi);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
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

		public bool PurgePages(IList<Article> inpages)
		{
			List<string> pagenames = inpages.Select(page => page.title).ToList();
			return PurgePages(pagenames);
		}

		public bool PurgePages(IList<string> inpages)
		{
			if (inpages.Count == 0) return true;

			//Encode page names
			for (int c = 0; c < inpages.Count; c++) inpages[c] = UrlEncode(inpages[c]);

			//Download stream
			string parameters = "format=json" +
				"&action=purge" +
				"&titles=" + string.Join("|", inpages) +
				"&forcelinkupdate";
			HttpWebRequest request = CreateWebRequest(UrlApi);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, parameters)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);

			return true;
		}

		/// <summary>
		/// Searches for entities with the specified title.
		/// </summary>
		public string[] SearchEntities(string query)
		{
			//Download stream
			Uri url = new Uri(
				UrlApi +
				"?format=json" +
				"&action=wbsearchentities" +
				"&type=item" + 
				"&language=en" +
				"&search=" + UrlEncode(query));
			HttpWebRequest request = CreateWebRequest(url);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
			object[] search = (object[])deser["search"];
			string[] results = new string[search.Length];
			for (int c = 0; c < search.Length; c++)
			{
				results[c] = (string)((Dictionary<string, object>)search[c])["id"];
			}

			return results;
		}

		public bool CreateEntityClaim(Entity entity, string property, string value, string summary, bool bot)
		{
			//Download stream
			string data = "format=json" +
				"&action=wbcreateclaim" +
				"&entity=" + entity.id +
				"&snaktype=value" +
				"&property=" + property +
				"&value=\"" + UrlEncode(value) + "\"" +
				"&summary=" + UrlEncode(summary) +
				(bot ? "&bot=1" : "") +
				"&token=" + UrlEncode(GetCsrfToken()) +
				"&assert=bot";

			HttpWebRequest request = CreateWebRequest(UrlApi);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
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
			if (ids.Count == 0) return new Entity[0];

			//Encode page names
			for (int c = 0; c < ids.Count; c++) ids[c] = UrlEncode(ids[c]);

			//Download stream
			Uri url = new Uri(
				UrlApi +
				"?format=json" +
				"&action=wbgetentities" +
				"&ids=" + string.Join("|", ids));
			HttpWebRequest request = CreateWebRequest(url);

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

		public IEnumerable<Contribution> GetContributions(string username, string startTime, string endTime)
		{
			//Encode page names
			username = UrlEncode(username);
			
			string basedata = "format=json" +
				"&action=query" +
				"&list=usercontribs" +
				"&ucuser=" + username +
				"&ucstart=" + startTime +
				"&ucend=" + endTime +
				"&uclimit=5000";
			string data = basedata + "&continue=";

			bool uccontinue = false;

			do
			{
				HttpWebRequest request = CreateWebRequest(UrlApi);

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)deser["query"])["usercontribs"])
				{
					yield return new Contribution(page);
				}

				uccontinue = deser.ContainsKey("continue");
				if (uccontinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					data = basedata;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						data += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (uccontinue);
		}

		public bool UploadFromWeb(Article newpage, string url, string summary, bool bot)
		{
			string data =
                "action=upload" +
                "&filename=" + UrlEncode(newpage.title) + //title?
                "&format=json" +
                "&summary=" + UrlEncode(summary) +
				"&url=" + UrlEncode(url) +
                (bot ? "&bot=1" : "") +
                "&starttimestamp=" + UrlEncode(newpage.starttimestamp) +
                "&token=" + UrlEncode(GetCsrfToken()) +
				"&ignorewarnings=1" +
				"&assert=bot";

			if (newpage.revisions != null && newpage.revisions.Length > 0)
			{
				data += "&text=" + UrlEncode(newpage.revisions[0].text);
			}

			HttpWebRequest request = CreateWebRequest(UrlApi);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
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
        public bool UploadFromLocal(Article newpage, string path, string summary, bool bot)
        {
            //Download stream
            Dictionary<string, string> data = new Dictionary<string, string>();
            data["format"] = "json";
            data["action"] = "upload";
            data["filename"] = newpage.title;
            data["token"] = GetCsrfToken();
            data["ignorewarnings"] = "1";
			data["summary"] = summary;
			if (bot) data["bot"] = "1";

            if (newpage.revisions != null && newpage.revisions.Length > 0)
            {
                data["text"] = newpage.revisions[0].text;
            }

            HttpWebRequest request = CreateWebRequest(UrlApi);

            string filetype = "application/octet-stream";
			switch (Path.GetExtension(path))
            {
                case ".gif": filetype = "image/gif"; break;
                case ".jpg": filetype = "image/jpeg"; break;
                case ".png": filetype = "image/png"; break;
                case ".bmp": filetype = "image/bmp"; break;
            }

			byte[] rawfile;
			using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open)))
			{
				rawfile = reader.ReadBytes((int)reader.BaseStream.Length);
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
					return false;
				}
			}

            Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            if (deser.ContainsKey("error"))
            {
				Dictionary<string, object> error = (Dictionary<string, object>)deser["error"];
				throw new WikimediaException(error["code"] + ": " + error["info"]);
			}
            return true;
        }

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
        public object[] GetDuplicateFiles(byte[] file)
        {
            SHA1 algo = SHA1.Create();
            byte[] shaData = algo.ComputeHash(file);
            StringBuilder shaHex = new StringBuilder();
            for (int i = 0; i < shaData.Length; i++)
                shaHex.Append(shaData[i].ToString("x2"));

            //Download stream
            Uri url = new Uri(
                UrlApi + "?format=json&action=query&list=allimages&prop=imageinfo&aisha1=" + shaHex);
            HttpWebRequest request = CreateWebRequest(url);

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
            //Download stream
            Uri url = new Uri(
                UrlApi + "?format=json&action=query&meta=tokens&type=csrf");
            HttpWebRequest request = CreateWebRequest(url);

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
		/// Recursively traverse all pages in a category.
		/// </summary>
		public IEnumerable<Article> GetCategoryPagesRecursive(string category, int maxDepth = int.MaxValue, HashSet<string> alreadyHandledSubcats = null)
		{
			if (maxDepth <= 0) yield break;
			if (alreadyHandledSubcats == null)
			{
				alreadyHandledSubcats = new HashSet<string>();
			}
			alreadyHandledSubcats.Add(category);
			foreach (Article article in GetCategoryPages(category))
			{
				if (article.GetNamespace() == "Category")
				{
					if (!alreadyHandledSubcats.Contains(article.title))
					{
						foreach (Article art2 in GetCategoryPagesRecursive(article.title, maxDepth-1, alreadyHandledSubcats))
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

		public IEnumerable<Article> GetCategoryPages(string category, string startFrom = "")
		{
			return GetCategoryEntries(category, "page|subcat|file", startFrom);
		}

		public IEnumerable<Article> GetCategorySubcats(string category, string startFrom = "")
		{
			return GetCategoryEntries(category, "subcat", startFrom);
		}

		public IEnumerable<Article> GetCategoryFiles(string category, string startFrom = "")
		{
			return GetCategoryEntries(category, "file", startFrom);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cmtype">Type of entries to return. Default: "page|subcat|file"</param>
		public IEnumerable<Article> GetCategoryEntries(string category, string cmtype, string startFrom = "")
		{
			string basedata =
				"action=query" +
				"&list=categorymembers" +
				"&format=json" +
				"&cmtype=" + cmtype +
				"&cmtitle=" + UrlEncode(category) +
				"&cmlimit=500";
			if (!string.IsNullOrEmpty(startFrom))
			{
				basedata += "&cmstartsortkeyprefix=" + UrlEncode(startFrom);
			}
			string data = basedata + "&continue=";

			bool cmcontinue = false;

			do
			{
				HttpWebRequest request = CreateWebRequest(UrlApi);

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)deser["query"])["categorymembers"])
				{
					yield return new Article(page);
				}

				cmcontinue = deser.ContainsKey("continue");
				if (cmcontinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					data = basedata;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						data += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (cmcontinue);
		}

        public Article[] GetCategoryPagesFlat(string category)
        {
			return GetCategoryPages(category).ToArray();
        }

        internal static string UrlEncode(string str)
        {
            return System.Web.HttpUtility.UrlEncode(str);
        }
    }

	public class Object
	{
		public int pageid;
		public int ns;
		public string title;
		public int lastrevid;
		public bool missing = false;

		public Dictionary<string, object> raw;

		public Object()
		{

		}

		public Object(Dictionary<string, object> json)
		{
			raw = json;
			if (json.ContainsKey("ns"))
			{
				ns = (int)(json["ns"]);
			}
			if (json.ContainsKey("title"))
			{
				title = (string)(json["title"]);
			}
			if (json.ContainsKey("missing"))
			{
				missing = json.ContainsKey("missing");
			}
			if (!missing)
			{
				pageid = (int)(json["pageid"]);
				if (json.ContainsKey("lastrevid"))
					lastrevid = (int)(json["lastrevid"]);
			}
		}

		/// <summary>
		/// Returns the article title without the namespace.
		/// </summary>
		public string GetTitle()
		{
			int colon = title.IndexOf(':');
			if (colon > 0)
			{
				return title.Substring(colon + 1);
			}
			else
			{
				return title;
			}
		}

		/// <summary>
		/// Returns the article's namespace.
		/// </summary>
		public string GetNamespace()
		{
			int colon = title.IndexOf(':');
			if (colon > 0)
			{
				return title.Substring(0, colon);
			}
			else
			{
				return "";
			}
		}
	}

	public class Entity : Object
	{
		public string modified;
		public string id;
		public string type;

		//indexed by language
		public Dictionary<string, string[]> aliases;
		public Dictionary<string, string> labels;
		public Dictionary<string, string> descriptions;

		public Dictionary<string, Claim[]> claims;

		public Dictionary<string, string> sitelinks;

		public Entity()
		{

		}

		public Entity(Dictionary<string, object> json)
			: base(json)
		{
			if (json.ContainsKey("modified"))
				modified = (string)json["modified"];
			if (json.ContainsKey("id"))
				id = (string)json["id"];
			if (json.ContainsKey("type"))
				type = (string)json["type"];

			if (json.ContainsKey("aliases"))
				aliases = ParseLanguageValueArray((Dictionary<string, object>)json["aliases"]);
			if (json.ContainsKey("labels"))
				labels = ParseLanguageValue((Dictionary<string, object>)json["labels"]);
			if (json.ContainsKey("descriptions"))
				descriptions = ParseLanguageValue((Dictionary<string, object>)json["descriptions"]);

			claims = new Dictionary<string, Claim[]>();
			if (json.ContainsKey("claims"))
			{
				Dictionary<string, object> claimData = (Dictionary<string, object>)json["claims"];
				foreach (KeyValuePair<string, object> kv in claimData)
				{
					object[] claimJsonArray = (object[])kv.Value;
					Claim[] claimArray = new Claim[claimJsonArray.Length];
					for (int c = 0; c < claimArray.Length; c++)
					{
						claimArray[c] = new Claim((Dictionary<string, object>)claimJsonArray[c]);
					}
					claims[kv.Key] = claimArray;
				}
			}

			sitelinks = new Dictionary<string, string>();
			if (json.ContainsKey("sitelinks"))
			{
				Dictionary<string, object> sitelinkData = (Dictionary<string, object>)json["sitelinks"];
				foreach (KeyValuePair<string, object> kv in sitelinkData)
				{
					Dictionary<string, object> valueDict = (Dictionary<string, object>)kv.Value;
					if (valueDict.ContainsKey("title"))
					{
						sitelinks[kv.Key] = (string)valueDict["title"];
					}
				}
			}
		}

		private static Dictionary<string, string[]> ParseLanguageValueArray(
			Dictionary<string, object> json)
		{
			Dictionary<string, string[]> result = new Dictionary<string, string[]>();
			foreach (KeyValuePair<string, object> kv in json)
			{
				object[] aliasObjList = (object[])kv.Value;
				string[] aliasList = new string[aliasObjList.Length];
				for (int c = 0; c < aliasObjList.Length; c++)
				{
					Dictionary<string, object> arrayItem = (Dictionary<string, object>)aliasObjList[c];
					aliasList[c] = (string)arrayItem["value"];
				}
				result[kv.Key] = aliasList;
			}
			return result;
		}

		private static Dictionary<string, string> ParseLanguageValue(
			Dictionary<string, object> json)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			foreach (KeyValuePair<string, object> kv in json)
			{
				result[kv.Key] = (string)((Dictionary<string, object>)kv.Value)["value"];
			}
			return result;
		}

		public bool HasExactName(string name, bool caseSensitive)
		{
			if (string.Compare(title, name, !caseSensitive) == 0)
				return true;

			if (aliases != null)
			{
				foreach (string[] valueList in aliases.Values)
				{
					foreach (string value in valueList)
					{
						if (string.Compare(value, name, !caseSensitive) == 0)
							return true;
					}
				}
			}

			if (labels != null)
			{
				foreach (string value in labels.Values)
				{
					if (string.Compare(value, name, !caseSensitive) == 0)
						return true;
				}
			}

			return false;
		}

		public bool HasClaim(string id)
		{
			return claims != null && claims.ContainsKey(id)
				&& claims[id].Length > 0 && claims[id][0].mainSnak.datavalue != null;
		}

		public Entity GetClaimValueAsEntity(string property, WikiApi api)
		{
			return claims[property][0].mainSnak.GetValueAsEntity(api);
		}

		public int GetClaimValueAsEntityId(string property)
		{
			return claims[property][0].mainSnak.GetValueAsEntityId();
		}

		public Entity[] GetClaimValuesAsEntity(string property, WikiApi api)
		{
			Claim[] subclaims = claims[property];
			Entity[] result = new Entity[subclaims.Length];
			for (int c = 0; c < subclaims.Length; c++)
				result[c] = subclaims[c].mainSnak.GetValueAsEntity(api);
			return result;
		}

		public string GetClaimValueAsString(string property)
		{
			return claims[property][0].mainSnak.GetValueAsString();
		}

		public string[] GetClaimValuesAsString(string property)
		{
			Claim[] subclaims = claims[property];
			string[] result = new string[subclaims.Length];
			for (int c = 0; c < subclaims.Length; c++)
				result[c] = subclaims[c].mainSnak.GetValueAsString();
			return result;
		}

		public string GetClaimValueAsGender(string property)
		{
			return claims[property][0].mainSnak.GetValueAsGender();
		}

		public DateTime GetClaimValueAsDate(string property)
		{
			return claims[property][0].mainSnak.GetValueAsDate();
		}
	}

	public class Snak
	{
		public string snaktype;
		public string property;
		public string datatype;
		public Dictionary<string, object> datavalue;

		public Snak(Dictionary<string, object> json)
		{
			snaktype = (string)json["snaktype"];
			property = (string)json["property"];
			if (json.ContainsKey("datatype"))
				datatype = (string)json["datatype"];
			if (json.ContainsKey("datavalue"))
				datavalue = (Dictionary<string, object>)json["datavalue"];
		}

		/*public string GetSerialized()
		{
			//TODO:
		}*/

		public Entity GetValueAsEntity(WikiApi api)
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			string pagename = "Q" + (int)entityValue["numeric-id"];
			return api.GetEntity(pagename);
		}

		public int GetValueAsEntityId()
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			return (int)entityValue["numeric-id"];
		}

		public string GetValueAsString()
		{
			return (string)datavalue["value"];
		}

		public string GetValueAsGender()
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];
			switch ((int)entityValue["numeric-id"])
			{
				case 6581072:
				case 1052281:
					return "female";
				case 6581097:
				case 2449503:
					return "male";
				default:
					return "";
			}
		}

		public DateTime GetValueAsDate()
		{
			Dictionary<string, object> entityValue = (Dictionary<string, object>)datavalue["value"];

			int precision = (int)entityValue["precision"];
			return new DateTime((string)entityValue["time"], precision);
		}
	}

	public class Claim
	{
		public string id;
		public Snak mainSnak;
		public string type;
		public string rank;

		public Claim(Dictionary<string, object> json)
		{
			id = (string)json["id"];
			mainSnak = new Snak((Dictionary<string, object>)json["mainsnak"]);
			type = (string)json["type"];
			rank = (string)json["rank"];
		}

		public string GetSerialized()
		{
			return "{\"id\":\"Q2$5627445f-43cb-ed6d-3adb-760e85bd17ee\",\"type\":\"" + type + "\",\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P1\",\"datavalue\":{\"value\":\"City\",\"type\":\"string\"}}}";
		}
	}

    public class Article : Object
    {
		/// <summary>
		/// A list of changes that have been made to this article.
		/// </summary>
		public List<string> Changes = new List<string>();
		public bool Dirty = false;

        public string touched;
        public int counter;
        public int length;
        public string starttimestamp;
        public string edittoken;
        public Revision[] revisions;
		public Article[] links;

		public Article()
		{

		}

		public Article(string title)
		{
			this.title = title;
		}

		public Article(Dictionary<string, object> json)
			: base(json)
		{
			if (json.ContainsKey("starttimestamp"))
				starttimestamp = (string)(json["starttimestamp"]);
			if (json.ContainsKey("edittoken"))
				edittoken = (string)(json["edittoken"]);
			if (!missing)
			{
				if (json.ContainsKey("touched"))
					touched = (string)(json["touched"]);
				if (json.ContainsKey("length"))
					length = (int)(json["length"]);
				if (json.ContainsKey("revisions"))
				{
					object[] revisionsJson = (object[])(json["revisions"]);
					revisions = new Revision[revisionsJson.Length];
					for (int c = 0; c < revisions.Length; c++)
					{
						Dictionary<string, object> revJson = (Dictionary<string, object>)revisionsJson[c];
						revisions[c] = new Revision(revJson);
					}
				}
				if (json.ContainsKey("links"))
				{
					links = ReadArticleArray(json, "links");
				}
			}
		}

		private static Article[] ReadArticleArray(Dictionary<string, object> json, string key)
		{
			object[] articlesJson = (object[])(json[key]);
			Article[] articles = new Article[articlesJson.Length];
			for (int c = 0; c < articles.Length; c++)
			{
				Dictionary<string, object> revJson = (Dictionary<string, object>)articlesJson[c];
				articles[c] = new Article(revJson);
			}
			return articles;
		}

		/// <summary>
		/// Returns an enumerator over all pages that link to this one.
		/// </summary>
		public IEnumerable<Article> GetLinksHere(WikiApi api)
		{
			//Download stream
			string basedata = "format=json" +
				"&action=query" +
				"&titles=" + WikiApi.UrlEncode(title) +
				"&prop=linkshere" +
				"&lhlimit=5000";

			string data = basedata + "&continue=";

			bool lhcontinue = false;

			do
			{
				HttpWebRequest request = api.CreateWebRequest();

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(request, data)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
				Dictionary<string, object> query = (Dictionary<string, object>)deser["query"];
				Dictionary<string, object> pages = (Dictionary<string, object>)query["pages"];
				foreach (KeyValuePair<string, object> page in pages)
				{
					Dictionary<string, object> jsonData = (Dictionary<string, object>)page.Value;
					if (jsonData.ContainsKey("invalid"))
					{
						continue;
					}
					foreach (Dictionary<string, object> linkhere in (object[])jsonData["linkshere"])
					{
						yield return new Article(linkhere);
					}
				}

				lhcontinue = deser.ContainsKey("continue");
				if (lhcontinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)deser["continue"];
					data = basedata;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						data += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (lhcontinue);
		}

		public static bool IsNullOrEmpty(Article article)
		{
			return article == null || article.missing || article.revisions == null
				|| article.revisions.Length == 0;
		}

		public string GetEditSummary()
		{
			StringBuilder summary = new StringBuilder();
			for (int c = 0; c < Changes.Count; c++)
			{
				summary.Append(Changes[c]);
				if (c < Changes.Count - 1)
				{
					summary.Append(", ");
				}
			}
			return summary.ToString();
		}
    }

	public class Contribution
	{
		public string user;
		public int pageid;
		public int revid;
		public int ns;
		public string title;
		public string timestamp;
		public string comment;

		public Contribution()
		{

		}

		public Contribution(Dictionary<string, object> json)
		{
			object value;

			if (json.TryGetValue("user", out value))
			{
				user = (string)value;
			}
			if (json.TryGetValue("pageid", out value))
			{
				pageid = (int)value;
			}
			if (json.TryGetValue("revid", out value))
			{
				revid = (int)value;
			}
			if (json.TryGetValue("ns", out value))
			{
				ns = (int)value;
			}
			if (json.TryGetValue("title", out value))
			{
				title = (string)value;
			}
			if (json.TryGetValue("timestamp", out value))
			{
				timestamp = (string)value;
			}
			if (json.TryGetValue("comment", out value))
			{
				comment = (string)value;
			}
		}
	}

	public class Revision
	{
		public string contentformat;
		public string contentmodel;
		public string user;
		public string text;

		public Revision()
		{

		}

		public Revision(Dictionary<string, object> json)
		{
			object value;

			if (json.TryGetValue("contentformat", out value))
			{
				contentformat = (string)value;
			}
			if (json.TryGetValue("contentmodel", out value))
			{
				contentmodel = (string)value;
			}
			if (json.TryGetValue("user", out value))
			{
				user = (string)value;
			}
			if (json.TryGetValue("*", out value))
			{
				text = (string)value;
			}
		}
	}
}
