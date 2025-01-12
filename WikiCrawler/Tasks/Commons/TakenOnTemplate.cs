using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MediaWiki;

namespace WikiCrawler
{
	/// <summary>
	/// Adds "Template:Taken on" to the dates of appropriate files in specified categories.
	/// </summary>
	public class TakenOnTemplate
	{
		private static string[] categories = new string[]
        {
            "Category:Aerial pictures by User:Nyttend",
            "Category:Building-centered pictures by User:Nyttend",
            "Category:Community pictures by User:Nyttend",
            "Category:Highway pictures by User:Nyttend",
            "Category:Miscellaneous images by User:Nyttend",
            "Category:Portraits by User:Nyttend",
            "Category:Scenery pictures by User:Nyttend",
            "Category:Signs by User:Nyttend"
        };

		private static StreamWriter errorLog;
		private static string currentArticle;

		private static void LogError(string message)
		{
			Console.WriteLine("!!! " + message);

			errorLog.WriteLine(currentArticle);
			errorLog.WriteLine("!!! " + message);
		}

		public static void Do()
		{
			Console.WriteLine("Logging in...");
			Api Api = new Api(new Uri("https://commons.wikimedia.org/"));
			Api.AutoLogIn();

			int maxArticles = int.MaxValue;

			/*StreamWriter files = new StreamWriter(new FileStream("C:/files.txt", FileMode.OpenOrCreate), Encoding.Unicode);
			foreach (string cat in categories)
			{
				Console.WriteLine(cat);
				articles = WikimediaApi.GetCategoryPages(cat);
				foreach (Wikimedia.Article art in articles)
				{
					files.WriteLine(art.title);
				}
			}
			files.Close();

			return;*/

			try
			{
				errorLog = new StreamWriter(new FileStream("C:/problems.txt", FileMode.Append));

				using (StreamReader files = new StreamReader(new FileStream("C:/files.txt", FileMode.Open)))
				{
					while (!files.EndOfStream)
					{
						//Determine the article to process
						string file = files.ReadLine();
						if (string.IsNullOrEmpty(file)) continue;

						Article a = new Article() { title = file };
						currentArticle = a.title;
						Console.WriteLine(a.title);

						//Fetch it
						Article art = Api.GetPage(a, prop: Api.BuildParameterList(Prop.info, Prop.revisions, Prop.imageinfo), iiprop: IIProp.commonmetadata); //&iimetadataversion=2
						if (art.revisions != null && art.revisions.Any())
						{
							string text = art.revisions.First().text;

							//TODO: ensure date is actually in an infobox

							//Scan for date
							string dateMarker = "|date=";
							int dateStart = -1;
							int dateEnd = -1;
							string date = null;
							for (int c = 0; c < text.Length; c++)
							{
								if (dateStart < 0)
								{
									if (text.MatchAt(dateMarker, c))
									{
										dateStart = c + 1;
									}
								}
								else
								{
									if (text[c] == '|')
									{
										dateEnd = c - 1;
										while (text[dateEnd] == '\n' || text[dateEnd] == '\r')
										{
											//this is to preserve the newlines on replace
											dateEnd--;
										}
										date = text.Substring(dateStart, c - dateStart).Trim();
										break;
									}
								}
							}

							Dictionary<string, object> metadata = Api.GetCommonMetadata(art.raw);

							string metadate = "";
							if (metadata.ContainsKey("DateTimeOriginal"))
								metadate = ((string)metadata["DateTimeOriginal"]).Split()[0].Replace(':', '-');
							//else if (metadata.ContainsKey("DateTime"))
							//    metadate = ((string)metadata["DateTime"]).Split()[0].Replace(':', '-');

							if (dateStart < 0)
							{
								LogError("no infobox or no date param in infobox");
							}
							else
							{
								if (string.IsNullOrEmpty(date))
									date = metadate;

								//Get new date
								string newdate, yyyymmdd;
								if (!ReplaceDate(date, metadate, out newdate, out yyyymmdd))
								{
									//Add bad date category
									string newtext = WikiUtils.AddCategory("Category:Files with no machine-readable date", text);
									if (newtext != text)
									{
										art.revisions[0].text = newtext;
										Api.EditPage(art, "bot: cannot understand infobox date", minor: true);
									}
								}
								else if (!string.IsNullOrEmpty(newdate))
								{
									//Reconstruct and submit page
									string newtext = text.Substring(0, dateStart) + newdate + text.Substring(dateEnd + 1);
									if (!string.IsNullOrEmpty(yyyymmdd))
										newtext = WikiUtils.RemoveCategory("Category:Photographs taken on " + yyyymmdd, newtext);

									art.revisions[0].text = newtext;
									Api.EditPage(art, "bot: adding appropriate templates to infobox date", minor: true);

									if (newtext.Contains("[[Category:Photographs taken on"))
									{
										LogError("has taken-on category that doesn't match date");
									}
								}
							}
						}
						else
						{
							LogError("no revisions");
						}

						maxArticles--;
						if (maxArticles <= 0) break;

						if ((Directory.Exists(@"C:\Users\Brian\Documents\My Dropbox\Public")
							&& File.Exists(@"C:\Users\Brian\Documents\My Dropbox\Public\STOP.txt"))
							|| (Directory.Exists(@"C:\Users\Brian\Dropbox\Public")
							&& File.Exists(@"C:\Users\Brian\Dropbox\Public\STOP.txt")))
							break;
					}
				}
			}
			finally
			{
				//Write out progress
				StreamWriter files2 = new StreamWriter(new FileStream("takenon_progress.txt", FileMode.OpenOrCreate), Encoding.Unicode);
				//TODO:
				files2.Close();

				errorLog.Close();
			}
		}

		private static Dictionary<string, string> monthnames = new Dictionary<string, string>
		{
			{ "jan", "01" }, { "january", "01" },
			{ "feb", "02" }, { "february", "02" },
			{ "mar", "03" }, { "march", "03" },
			{ "apr", "04" }, { "april", "04" },
			{ "may", "05" },
			{ "jun", "06" }, { "june", "06" },
			{ "jul", "07" }, { "july", "07" },
			{ "aug", "08" }, { "august", "08" },
			{ "sep", "09" }, { "sept", "09" }, { "september", "09" },
			{ "oct", "10" }, { "october", "10" },
			{ "nov", "11" }, { "november", "11" },
			{ "dec", "12" }, { "december", "12" },
		};

		private static string sepClass = @"/\-\. \\";
		private static RegexOptions opt = RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase;
		private static Regex[] s_Dates = new Regex[]
		{
			new Regex(@"^(?<y>\d\d\d\d)$", opt), //YYYY
			new Regex(@"^(?<y>\d\d\d\d)["+sepClass+@"](?<m>\d\d)$", opt), //YYYY-MM
			new Regex(@"^(?<y>\d\d\d\d)["+sepClass+@"](?<m>\d\d)["+sepClass+@"](?<d>\d\d)$", opt), //YYYY-MM-DD
			//new Regex(@"^(?<d>\d\d)["+sepClass+@"](?<m>\d\d)["+sepClass+@"](?<y>\d\d\d\d)$", opt), //DD-MM-YYYY
			new Regex(@"^(?<m>[a-zA-Z]+)\s+(?<y>\d\d\d\d)$", opt), //NAME YYYY
			new Regex(@"^(?<m>[a-zA-Z]+)\s+(?<d>\d\d?)(st)?(th)?(nd)?(rd)?(,)?\s+(?<y>\d\d\d\d)$", opt), //NAME DD, YYYY
			new Regex(@"^(?<d>\d\d?)(st)?(th)?(nd)?(rd)?( of)?\s+(?<m>[a-zA-Z]+)(,)?\s+(?<y>\d\d\d\d)$", opt), //DD NAME, YYYY
		};

		private static string PadLeft(string a, int width, char c)
		{
			if (a.Length < width)
				a = new string(c, width - a.Length) + a;
			return a;
		}

		/// <summary>
		/// Tries to convert the date to ISO YYYY-MM-DD, YYYY-MM, or YYYY. Very conservative, prefers failing to being wrong.
		/// </summary>
		public static string DateToISO(string date, out bool hasDay)
		{
			date = date.Trim();

			//Try to match against each available regex
			string y = "";
			string m = "";
			string d = "";
			foreach (Regex rex in s_Dates)
			{
				Match match = rex.Match(date);
				if (match.Success)
				{
					y = match.Groups["y"].Value;

					m = match.Groups["m"].Value;
					foreach (string key in monthnames.Keys)
					{
						if (key.Equals(m, StringComparison.InvariantCultureIgnoreCase))
						{
							m = monthnames[key];
							break;
						}
					}

					d = match.Groups["d"].Value;

					break;
				}
			}

			//verify that these are valid numbers
			if (y.Length != 4 || m.Length > 2 || d.Length > 2)
			{
				hasDay = false;
				return "";
			}
			try
			{
				int.Parse(y);
				if (!string.IsNullOrEmpty(m))
					int.Parse(m);
				if (!string.IsNullOrEmpty(d))
					int.Parse(d);
			}
			catch (FormatException)
			{
				hasDay = false;
				return "";
			}

			hasDay = false;
			string res = "";
			if (!string.IsNullOrEmpty(y))
			{
				res += PadLeft(y, 4, '0');
				if (!string.IsNullOrEmpty(m))
				{
					res += "-" + PadLeft(m, 2, '0');
					if (!string.IsNullOrEmpty(d))
					{
						hasDay = true;
						res += "-" + PadLeft(d, 2, '0');
					}
				}
			}
			return res;
		}

		/// <summary>
		/// Given existing date information, determines what should go in the date param of the infobox.
		/// </summary>
		public static bool ReplaceDate(string date, string metadate, out string newcontent, out string yyyymmdd)
		{
			bool usingExif = false;
			yyyymmdd = "";

			if (string.IsNullOrEmpty(date))
			{
				if (string.IsNullOrEmpty(metadate))
				{
					//nothing to go on
					newcontent = "{{other date|?}}";
					return true;
				}
				else
				{
					//try metadate
					usingExif = true;
					date = metadate;
				}
			}

			//attempt to convert date to YYYY-MM-DD, YYYY-MM, or YYYY, with optional time
			bool hasDay;
			string isodate = DateToISO(date, out hasDay);

			if (string.IsNullOrEmpty(isodate))
			{
				if (!date.Equals("{{Taken on", StringComparison.InvariantCultureIgnoreCase)
					&& !date.Equals("{{Taken in", StringComparison.InvariantCultureIgnoreCase))
					LogError("'" + date + "' couldn't be parsed");
				newcontent = null;
				return false;
			}
			else if (hasDay)
			{
				//get rid of any time data for yyyymmdd
				//TODO: keep it afterward?
				yyyymmdd = isodate.Split(' ')[0];

				newcontent = "{{Taken on|" + isodate + "}}";
				if (usingExif) newcontent += " {{According to EXIF data}}";
				return true;
			}
			else
			{
				newcontent = "{{Taken in|" + isodate + "}}";
				if (usingExif) newcontent += " {{According to EXIF data}}";
				return true;
			}

			/*if (!string.IsNullOrEmpty(metadate) && metadate != date)
            {
				LogError("EXIF date doesn't match stated date. Using stated.");
            }*/
		}
	}
}
