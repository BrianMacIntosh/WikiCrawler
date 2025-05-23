using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace MediaWiki
{
	/// <summary>
	/// Contains methods for interacting with the Wikimedia web API.
	/// </summary>
	public class Api
	{
		public static string UserAgent = "BMacZeroBot (brianamacintosh@gmail.com)";

		public readonly Uri Domain;
		public readonly Uri UrlApi;

		private CookieContainer m_cookies;

		private static JavaScriptSerializer s_jsonSerializer;

		private static string s_edittoken;

		static Api()
		{
			s_jsonSerializer = new JavaScriptSerializer();
			s_jsonSerializer.MaxJsonLength = 16000000;
		}

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
			Console.WriteLine("Logging in to '" + Domain + "'...");
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
			LogApiRequest("login", lgname);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> result = ParseResponse(json);
			Dictionary<string, object> login = (Dictionary<string, object>)result["login"];
			if (((string)login["result"]).Equals("Success"))
			{
				return true;
			}
			else if (((string)login["result"]).Equals("NeedToken"))
			{
				//Send request again, adding lgtoken from "token"
				baseQuery += "&lgtoken=" + UrlEncode((string)login["token"]);
				LogApiRequest("login-lgtoken", lgname);

				//Read response
				using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
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
		/// If the article is redirected, gets and returns the redirect target. Otherwise, returns null.
		/// </summary>
		public Article GetRedirectTarget(Article article)
		{
			Match redirectMatch = s_redirectRegex.Match(article.revisions[0].text);
			if (redirectMatch.Success)
			{
				//TODO: get same props original article asked for
				return GetPage(PageTitle.Parse(redirectMatch.Groups[1].Value));
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// If the article is redirected, follows redirects until it isn't.
		/// </summary>
		public Article FollowRedirects(Article article)
		{
			//TODO: loop detection
			Match redirectMatch = s_redirectRegex.Match(article.revisions[0].text);
			if (redirectMatch.Success)
			{
				//TODO: get same props original article asked for
				article = GetPage(PageTitle.Parse(redirectMatch.Groups[1].Value));
				return FollowRedirects(article);
			}
			else
			{
				return article;
			}
		}
		private static readonly Regex s_redirectRegex = new Regex(@"#REDIRECT\s*\[\[(.+)\]\]\s*");

		/// <summary>
		/// Returns the Wiki text of the specified page
		/// </summary>
		public Article GetPage(Article page,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified,
			string ppprop = "",
			bool redirects = false)
		{
			return GetPage(page.title, redirects: redirects, prop: prop,
				iiprop: iiprop, iilimit: iilimit,
				rvprop: rvprop, rvlimit: rvlimit,
				ppprop: ppprop);
		}

		/// <summary>
		/// Returns the Wiki text of the specified page
		/// </summary>
		public Article GetPage(PageTitle title,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified,
			string rvstart = "",
			string rvend = "",
			string rvdir = "",
			string ppprop = "",
			string clshow = "",
			int cllimit = Limit.Max,
			string cldir = "",
			string iwprefix = "",
			bool redirects = false)
		{
			return GetPages(new PageTitle[] { title }, prop: prop, redirects: redirects,
				iiprop: iiprop, iilimit: iilimit,
				rvprop: rvprop, rvlimit: rvlimit, rvstart: rvstart, rvend: rvend, rvdir: rvdir,
				ppprop: ppprop,
				clshow: clshow, cllimit: cllimit, cldir: cldir, iwprefix: iwprefix).FirstOrDefault();
		}

		/// <summary>
		/// Returns the Wiki text for all of the specified pages
		/// </summary>
		public IEnumerable<Article> GetPages(IList<Article> articles,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified)
		{
			return GetPages(
				articles.Select(art => art.title).ToList(),
				prop: prop,
				iiprop: iiprop, iilimit: iilimit,
				rvprop: rvprop, rvlimit: rvlimit);
		}

		/// <summary>
		/// Returns the Wiki text for all of the specified pages
		/// </summary>
		public IEnumerable<Article> GetPages(IList<PageTitle> titles,
			string prop = "info|revisions",
			string iiprop = "",
			int iilimit = Limit.Unspecified,
			string rvprop = "content",
			int rvlimit = Limit.Unspecified,
			string rvstart = "",
			string rvend = "",
			string rvdir = "",
			string ppprop = "",
			string clshow = "",
			int cllimit = Limit.Max,
			string cldir = "",
			string iwprefix = "",
			bool redirects = false)
		{
			if (titles.Count == 0) yield break;

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
			if (!string.IsNullOrEmpty(rvstart))
			{
				baseQuery += "&rvstart=" + UrlEncode(rvstart);
			}
			if (!string.IsNullOrEmpty(rvend))
			{
				baseQuery += "&rvend=" + UrlEncode(rvend);
			}
			if (!string.IsNullOrEmpty(rvdir))
			{
				baseQuery += "&rvdir=" + UrlEncode(rvdir);
			}
			if (rvlimit != Limit.Unspecified)
			{
				baseQuery += "&rvlimit=" + GetLimitParameter(rvlimit);
			}
			if (!string.IsNullOrEmpty(ppprop))
			{
				baseQuery += "&ppprop=" + UrlEncode(ppprop);
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
			if (!string.IsNullOrEmpty(iwprefix))
			{
				baseQuery += "&iwprefix=" + UrlEncode(iwprefix);
			}
			if (redirects)
			{
				baseQuery += "&redirects=1";
			}

			LogApiRequest("query", GetOneOrMany(titles));

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			if (json.Length > s_jsonSerializer.MaxJsonLength)
			{
				//TODO: break up request
			}
			Dictionary<string, object> result = ParseResponse(json);
			Dictionary<string, object> query = (Dictionary<string, object>)result["query"];
			if (query.TryGetValue("pages", out object pagesObj))
			{
				Dictionary<string, object> pages = (Dictionary<string, object>)pagesObj;
				foreach (KeyValuePair<string, object> page in pages)
				{
					Dictionary<string, object> jsonData = (Dictionary<string, object>)page.Value;
					if (!jsonData.ContainsKey("invalid"))
					{
						yield return new Article(jsonData);
					}
				}
			}
			else
			{
				ConsoleUtility.WriteLine(ConsoleColor.Yellow, "  GetPages returned no pages.");
			}
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
			//MD5 hashFn = MD5.Create();
			//byte[] hash = hashFn.ComputeHash(Encoding.UTF8.GetBytes(newpage.revisions[0].text.ToCharArray()));
			//string md5 = Convert.ToBase64String(hash);

			if (string.IsNullOrEmpty(newpage.edittoken))
				newpage.edittoken = GetCsrfToken();

			string baseQuery = "format=json"
				+ "&action=edit"
				+ "&title=" + UrlEncode(newpage.title.FullTitle)
				+ "&text=" + UrlEncode(newpage.revisions[0].text)
				+ "&summary=" + UrlEncode(summary)
				//+ "&md5=" + UrlEncode(md5)
				+ "&starttimestamp=" + UrlEncode(newpage.starttimestamp)
				+ "&token=" + UrlEncode(newpage.edittoken);
			if (!string.IsNullOrEmpty(tags))
			{
				baseQuery += "&tags=" + UrlEncode(tags);
			}
			if (bot)
			{
				baseQuery += "&bot";
				baseQuery += "&assert=bot";
			}
			else
			{
				baseQuery += "&assert=user";
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

			LogApiRequest("edit", newpage.title);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> result = ParseResponse(json);

			newpage.Dirty = false;
			newpage.Changes.Clear();
			return true;
		}

		/// <summary>
		/// Undoes the specified revision.
		/// </summary>
		public bool UndoRevision(int pageid, int revisionid, string summary = "", bool bot = true)
		{
			if (string.IsNullOrEmpty(s_edittoken))
				s_edittoken = GetCsrfToken();

			string baseQuery = "format=json"
				+ "&action=edit"
				+ "&pageid=" + pageid.ToString()
				+ "&undo=" + revisionid.ToString()
				+ "&summary=" + UrlEncode(summary)
				+ "&nocreate"
				+ "&token=" + UrlEncode(s_edittoken);
			if (bot)
			{
				baseQuery += "&bot";
				baseQuery += "&assert=bot";
			}

			LogApiRequest("edit-undo", pageid.ToString());

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> result = ParseResponse(json);

			return true;
		}

		/// <summary>
		/// Purges the cache for the specified pages.
		/// </summary>
		public bool PurgePages(IList<Article> inpages)
		{
			List<PageTitle> pagenames = inpages.Select(page => page.title).ToList();
			return PurgePages(pagenames);
		}

		/// <summary>
		/// Purges the cache for the specified pages.
		/// </summary>
		public bool PurgePages(IList<PageTitle> inpages)
		{
			if (inpages.Count == 0) return true;

			//Download stream
			string baseQuery = "format=json"
				+ "&action=purge"
				+ "&titles=" + UrlEncode(string.Join("|", inpages))
				+ "&forcelinkupdate";
			LogApiRequest("purge", GetOneOrMany(inpages));

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> result = ParseResponse(json);

			return true;
		}

		/// <summary>
		/// Searches for pages by a search query.
		/// </summary>
		/// <param name="srnamespace">Build with <see cref="Api.BuildNamespaceList(Namespace[])"/></param>
		public IEnumerable<Article> Search(string srsearch, string srnamespace = "", int srlimit = Limit.Max, string srwhat = "")
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
			if (!string.IsNullOrEmpty(srwhat))
			{
				baseQuery += "&srwhat=" + UrlEncode(srwhat);
			}
			string query = baseQuery + "&continue=";

			bool doContinue;
			do
			{
				LogApiRequest("query-search", srsearch);

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, query)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> result = ParseResponse(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)result["query"])["search"])
				{
					yield return new Article(page);
				}

				doContinue = result.ContainsKey("continue");
				if (doContinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)result["continue"];
					query = baseQuery;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						query += "&" + kv.Key + "=" + kv.Value.ToString();
					}
				}
			}
			while (doContinue);
		}

		/// <summary>
		/// Searches for entities with the specified title.
		/// </summary>
		public IEnumerable<QId> SearchEntities(string search, string language = "en")
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
			LogApiRequest("wbsearchentities", search);

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> result = ParseResponse(json);
			object[] searchJson = (object[])result["search"];
			for (int c = 0; c < searchJson.Length; c++)
			{
				yield return QId.Parse((string)((Dictionary<string, object>)searchJson[c])["id"]);
			}
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

			LogApiRequest("wbcreateclaim", property);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> result = ParseResponse(json);

			//HACK: create locally in case cached
			entity.claims.Add(property, new Claim[] { new Claim(value) });

			return true;
		}

		/// <summary>
		/// Returns the Wiki entity data of the specified page
		/// </summary>
		public Entity GetEntity(QId page)
		{
			try
			{
				IEnumerable<Entity> entities = GetEntities(new QId[] { page });
				if (entities != null)
				{
					return entities.FirstOrDefault();
				}
				else
				{
					return null;
				}
			}
			catch (WikimediaCodeException e)
			{
				if (e.Code == "no-such-entity")
				{
					return null;
				}
				else
				{
					throw;
				}
			}
		}

		/// <summary>
		/// Returns the Wiki entity data for all of the specified pages
		/// </summary>
		public IEnumerable<Entity> GetEntities(IList<QId> ids = null,
			string sites = "",
			string titles = "",
			bool? redirects = null,
			string props = "info|sitelinks|aliases|labels|descriptions|claims|datatype",
			string languages = "",
			bool? languageFallback = null,
			bool? normalize = null,
			string sitefilter = "")
		{
			if (ids.Count == 0) yield break;

			// maximum simultaneous request is 500 for bots on Wikidata
			//TODO: retest
			const int maxEntities = 500;
			if (ids.Count > maxEntities)
			{
				int index = 0;
				List<QId> idBuffer = new List<QId>(maxEntities);
				while (index < ids.Count)
				{
					for (int i = 0; i < maxEntities; i++)
					{
						if (index + i >= ids.Count) break;
						idBuffer.Add(ids[index + i]);
					}
					foreach (Entity intermediateEntity in GetEntities(idBuffer, sites, titles, redirects, props, languages, languageFallback, normalize, sitefilter))
					{
						yield return intermediateEntity;
					}
					index += maxEntities;
					idBuffer.Clear();
				}
				yield break;
			}

			string baseQuery = "format=json"
				+ "&action=wbgetentities";
			if (ids != null)
			{
				baseQuery += "&ids=" + UrlEncode(string.Join("|", ids));
			}
			if (!string.IsNullOrEmpty(sites))
			{
				baseQuery += "&sites=" + UrlEncode(sites);
			}
			if (!string.IsNullOrEmpty(titles))
			{
				baseQuery += "&titles=" + UrlEncode(titles);
			}
			if (redirects.HasValue)
			{
				baseQuery += "&redirects=" + (redirects.Value ? "yes" : "no");
			}
			if (!string.IsNullOrEmpty(props))
			{
				baseQuery += "&props=" + UrlEncode(props);
			}
			if (!string.IsNullOrEmpty(languages))
			{
				baseQuery += "&languages=" + UrlEncode(languages);
			}
			if (languageFallback.HasValue)
			{
				baseQuery += "&languageFallback=" + languageFallback.Value.ToString();
			}
			if (normalize.HasValue)
			{
				baseQuery += "&normalize=" + normalize.Value.ToString();
			}

			HttpWebRequest request = CreateApiRequest(baseQuery);
			LogApiRequest("wbgetentities", GetOneOrMany(ids));

			// Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> result = ParseResponse(json);
			if (!result.ContainsKey("entities"))
			{
				yield break;
			}
			Dictionary<string, object> entities = (Dictionary<string, object>)result["entities"];

			foreach (KeyValuePair<string, object> page in entities)
			{
				yield return new Entity((Dictionary<string, object>)page.Value);
			}
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
				LogApiRequest("query-usercontribs", ucuser);

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, query)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> result = ParseResponse(json);
				foreach (Dictionary<string, object> page in (object[])((Dictionary<string, object>)result["query"])["usercontribs"])
				{
					yield return new Contribution(page);
				}

				doContinue = result.ContainsKey("continue");
				if (doContinue)
				{
					Dictionary<string, object> continueData = (Dictionary<string, object>)result["continue"];
					query = baseQuery;
					foreach (KeyValuePair<string, object> kv in continueData)
					{
						query += "&" + kv.Key + "=" + (string)kv.Value;
					}
				}
			}
			while (doContinue);
		}

		private static Dictionary<string, InterwikiConfig> s_interwikiPrefixMap;

		/// <summary>
		/// Returns the interwiki map for this wiki.
		/// </summary>
		public Dictionary<string, InterwikiConfig> GetInterwikiMap()
		{
			if (s_interwikiPrefixMap == null)
			{
				s_interwikiPrefixMap = new Dictionary<string, InterwikiConfig>(StringComparer.OrdinalIgnoreCase);
				foreach (InterwikiConfig iwc in RawGetInterwikiMap())
				{
					s_interwikiPrefixMap.Add(iwc.prefix, iwc);
				}
			}
			return s_interwikiPrefixMap;
		}

		/// <summary>
		/// Returns the interwiki map for this wiki.
		/// </summary>
		private IEnumerable<InterwikiConfig> RawGetInterwikiMap(string sifilteriw = "")
		{
			string query = "format=json" +
				"&action=query" +
				"&meta=siteinfo" +
				"&siprop=interwikimap";
			if (!string.IsNullOrEmpty(sifilteriw))
			{
				query += "&sifilteriw=" + UrlEncode(sifilteriw);
			}

			LogApiRequest("query-interwikimap");

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, query)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> result = ParseResponse(json);
			foreach (Dictionary<string, object> iw in (object[])((Dictionary<string, object>)result["query"])["interwikimap"])
			{
				yield return new InterwikiConfig(iw);
			}

		}

		public bool UploadFromWeb(Article newpage, string url, string summary, bool bot = true)
		{
			string baseQuery = "format=json"
				+ "&action=upload"
				+ "&filename=" + UrlEncode(newpage.title.FullTitle)
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

			LogApiRequest("upload", newpage.title);

			//Read response
			string json;
			using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, baseQuery)))
			{
				json = read.ReadToEnd();
			}

			Dictionary<string, object> result = ParseResponse(json);

			return true;
		}

		/// <summary>
		/// Uploads media from the local computer.
		/// </summary>
		/// <returns>Success</returns>
		public bool UploadFromLocal(Article newpage, string path, string summary, bool bot = true)
		{
			if (!newpage.title.IsNamespace(PageTitle.NS_File))
			{
				throw new ArgumentException("Title is not in the File namespace.", "newpage");
			}

			//Download stream
			Dictionary<string, string> data = new Dictionary<string, string>();
			data["format"] = "json";
			data["action"] = "upload";
			data["filename"] = newpage.title.Name;
			data["token"] = GetCsrfToken();
			data["ignorewarnings"] = "1";
			data["summary"] = summary;
			data["comment"] = summary;
			if (bot) data["bot"] = "1";

			if (newpage.revisions != null && newpage.revisions.Length > 0)
			{
				data["text"] = newpage.revisions[0].text;
			}

			string filetype = MimeUtility.GetMimeFromExtension(Path.GetExtension(path));

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

			string finalResponseJson;

			if (rawfile.Length > 104857600)
			{
				Console.WriteLine("Using chunked upload.");

				int chunkSize = 1024 * 1024;
				int fileOffset = 0;

				data["filesize"] = rawfile.Length.ToString();
				data["stash"] = "1";

				//TODO: test me

				do
				{
					int thisChunkSize = Math.Min(chunkSize, rawfile.Length - fileOffset);
					data["offset"] = fileOffset.ToString();

					//TODO: error handling

					HttpWebRequest request = CreateApiRequest();
					LogApiRequest("upload-chunked", newpage.title);

					Console.Write("0%");

					// do filenames need to be unique?
					using (StreamReader read = new StreamReader(EasyWeb.Upload(request, data, newpage.title.Name, filetype, "chunk",
						rawfile, fileOffset, thisChunkSize)))
					{
						string responseJson = read.ReadToEnd();

						Dictionary<string, object> response = ParseResponse(responseJson);

						Dictionary<string, object> upload = (Dictionary<string, object>)response["upload"];
						string responseResult = (string)upload["result"];
						if (responseResult == "Success")
						{
							Console.WriteLine("\r100%");
							data["filekey"] = (string)upload["filekey"];
							break;
						}
						else if (responseResult == "Continue")
						{
							fileOffset = (int)upload["offset"];
							data["filekey"] = (string)upload["filekey"];
						}
						else
						{
							throw new WikimediaException("Chunked upload result was '" + responseResult + "'");
						}

						Console.Write("\r" + ((int)(100f * fileOffset / (float)rawfile.Length)).ToString() + "%");
					}
				}
				while (fileOffset < rawfile.Length);

				// commit completed upload
				{
					data.Remove("filesize");
					data.Remove("stash");
					data.Remove("offset");
					LogApiRequest("upload-finish", newpage.title);
					using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, data)))
					{
						finalResponseJson = read.ReadToEnd();
					}
				}
			}
			else
			{
				// single-part upload
				bool retry;
				do
				{
					retry = false;
					try
					{
						//Read response
						HttpWebRequest request = CreateApiRequest();
						LogApiRequest("upload", newpage.title);
						using (StreamReader read = new StreamReader(EasyWeb.Upload(request, data, newpage.title.Name, filetype, "file", rawfile)))
						{
							finalResponseJson = read.ReadToEnd();
						}
					}
					catch (WebException e)
					{
						if (e.Status == WebExceptionStatus.ProtocolError
							&& ((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.ServiceUnavailable)
						{
							System.Threading.Thread.Sleep(60000);
							retry = true;
							finalResponseJson = null; //HACK: suppress unassigned warning
						}
						else
						{
							throw e;
						}
					}
				} while (retry);
			}

			try
			{
				ParseResponse(finalResponseJson);
			}
			catch (WikimediaCodeException e)
			{
				if (e.Code == "verification-error")
				{
					if ((string)e.Details[0] == "filetype-mime-mismatch")
					{
						// attempt to automatically fix extension/mime mismatch
						string mime = (string)e.Details[2];
						string actualExt = MimeUtility.GetExtensionFromMime(mime);
						int extIndex = newpage.title.Name.LastIndexOf('.');
						newpage.title.Name = newpage.title.Name.Substring(0, extIndex) + actualExt;
						return UploadFromLocal(newpage, path, summary, bot);
					}
				}

				throw e;
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
			LogApiRequest("query-dupes");

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = ParseResponse(json);
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
			LogApiRequest("query-csrf");

			string json;
			using (StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request)))
			{
				json = read.ReadToEnd();
			}

			//Parse and read
			Dictionary<string, object> deser = ParseResponse(json);

			return (string)((Dictionary<string, object>)((Dictionary<string, object>)deser["query"])["tokens"])["csrftoken"];
		}

		/// <summary>
		/// Fetches contents for the specified unfetched articles.
		/// </summary>
		public IEnumerable<Article> FetchArticles(IEnumerable<Article> articles)
		{
			Article[] buffer = new Article[500];
			int bufferPtr = 0;

			// query for articles in batches of fixed size
			foreach (Article article in articles)
			{
				if (article.missing || article.revisions != null)
				{
					// already fetched
					yield return article;
				}

				if (bufferPtr < buffer.Length)
				{
					buffer[bufferPtr++] = article;
					continue;
				}

				foreach (Article file in GetPages(buffer, prop: "info|revisions"))
				{
					yield return file;
				}

				bufferPtr = 0;
				Array.Clear(buffer, 0, buffer.Length);
			}

			if (bufferPtr > 0)
			{
				// pick up the last incomplete batch
				Array.Resize(ref buffer, bufferPtr);
				foreach (Article file in GetPages(buffer, prop: "info|revisions"))
				{
					yield return file;
				}
			}
		}

		/// <summary>
		/// Returns all entries in the specified category and its descendents (depth-first).
		/// Page contents are not fetched.
		/// </summary>
		public IEnumerable<Article> GetCategoryEntriesRecursive(PageTitle category, int maxDepth = int.MaxValue,
			string cmtype = "",
			string cmstartsortkeyprefix = "")
		{
			return GetCategoryEntriesRecursive(category, maxDepth, new HashSet<PageTitle>(), cmtype, cmstartsortkeyprefix);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="cmstartsortkeyprefix">Only applies to the top-level category.</param>
		private IEnumerable<Article> GetCategoryEntriesRecursive(PageTitle category, int maxDepth, HashSet<PageTitle> alreadyHandledSubcats,
			string cmtype = "",
			string cmstartsortkeyprefix = "")
		{
			if (maxDepth <= 0) yield break;
			alreadyHandledSubcats.Add(category);
			foreach (Article article in GetCategoryEntries(category, cmtype: BuildParameterList(cmtype, CMType.subcat), cmstartsortkeyprefix: cmstartsortkeyprefix))
			{
				if (article.ns == Namespace.Category)
				{
					if (!alreadyHandledSubcats.Contains(article.title))
					{
						foreach (Article art2 in GetCategoryEntriesRecursive(article.title, maxDepth - 1, alreadyHandledSubcats))
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
			PageTitle cmtitle,
			string cmtype = "",
			string cmnamespace = "",
			int cmlimit = Limit.Max,
			string cmstartsortkeyprefix = "")
		{
			string baseQuery = "format=json"
				+ "&action=query"
				+ "&list=categorymembers"
				+ "&cmtitle=" + UrlEncode(cmtitle.FullTitle);
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

			bool doContinue;

			do
			{
				LogApiRequest("query-categorymembers", cmtitle.FullTitle);

				//Read response
				string json;
				using (StreamReader read = new StreamReader(EasyWeb.Post(CreateApiRequest, query)))
				{
					json = read.ReadToEnd();
				}

				Dictionary<string, object> deser = ParseResponse(json);
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
						if (kv.Key != "continue")
						{
							query += "&" + kv.Key + "=" + (string)kv.Value;
						}
					}
				}
			}
			while (doContinue);
		}

		internal static string UrlEncode(string str)
		{
			return System.Web.HttpUtility.UrlEncode(str);
		}

		private void LogApiRequest(string endpoint)
		{
			ConsoleUtility.WriteLine(ConsoleColor.DarkGray, "    API request '{0}' ({1})", endpoint, UrlApi);
		}

		private void LogApiRequest(string endpoint, object param)
		{
			ConsoleUtility.WriteLine(ConsoleColor.DarkGray, "    API request '{0}' ({1}): {2}", endpoint, UrlApi, param);
		}

		private Dictionary<string, object> ParseResponse(string responseJson)
		{
			Dictionary<string, object> response = (Dictionary<string, object>)s_jsonSerializer.DeserializeObject(responseJson);
			HandleErrorsWarnings(response);
			return response;
		}

		private void HandleErrorsWarnings(Dictionary<string, object> result)
		{
			if (result.ContainsKey("error"))
			{
				Dictionary<string, object> error = (Dictionary<string, object>)result["error"];
				throw new WikimediaCodeException(error);
			}
			if (result.ContainsKey("warnings"))
			{
				Dictionary<string, object> warnings = (Dictionary<string, object>)result["warnings"];
				foreach (KeyValuePair<string, object> kv in warnings)
				{
					if (kv.Key == "main") continue;
					Dictionary<string, object> value = (Dictionary<string, object>)kv.Value;
					ConsoleUtility.WriteLine(ConsoleColor.Yellow, "    API warning: {0}: {1}", kv.Key, value["*"]);
				}
			}
		}

		private static string GetOneOrMany<T>(IEnumerable<T> strings)
		{
			if (!strings.Any())
			{
				return "";
			}
			else if (strings.Count() > 1)
			{
				return "<multiple>";
			}
			else
			{
				return strings.First().ToString();
			}
		}
	}
}
