using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using WikiCrawler;

public abstract class BatchUploader : BatchTask
{
	protected Wikimedia.WikiApi Api = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));

	private HashSet<string> s_succeeded = new HashSet<string>();

	protected string PreviewDirectory
	{
		get { return Path.Combine(ProjectDataDirectory, "preview"); }
	}

	public BatchUploader(string key, ProjectConfig config)
		: base(key, config)
	{
		if (!Directory.Exists(PreviewDirectory))
			Directory.CreateDirectory(PreviewDirectory);

		Console.WriteLine("Logging in...");
		Credentials credentials = Configuration.LoadCredentials();
		Api.LogIn(credentials.Username, credentials.Password);

		// load already-succeeded uploads
		string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
		if (File.Exists(succeededFile))
		{
			string[] succeeded = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(succeededFile, Encoding.UTF8));
			foreach (string suc in succeeded)
			{
				s_succeeded.Add(suc);
			}
		}
	}

	/// <summary>
	/// Uploads all configured files.
	/// </summary>
	public void UploadAll()
	{
		string stopFile = Path.Combine(Configuration.DataDirectory, "STOP");

		try
		{
			foreach (string metadataFile in Directory.GetFiles(MetadataCacheDirectory))
			{
				string key = Path.GetFileNameWithoutExtension(metadataFile);
				Dictionary<string, string> metadata
					= JsonConvert.DeserializeObject<Dictionary<string, string>>(
						File.ReadAllText(metadataFile, Encoding.UTF8));

				Wikimedia.Article art = new Wikimedia.Article();
				art.title = GetTitle(key, metadata);
				art.revisions = new Wikimedia.Revision[1];
				art.revisions[0] = new Wikimedia.Revision();
				art.revisions[0].text = BuildPage(key, metadata);

				if (!m_config.allowUpload)
				{
					throw new UWashException("upload disabled");
				}

				string imagePath = GetUploadImagePath(key, metadata);

			reupload:
				bool uploadSuccess;
				try
				{
					uploadSuccess = Api.UploadFromLocal(art, imagePath, "(BOT) batch upload", true);
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

				if (File.Exists(stopFile))
				{
					File.Delete(stopFile);
					Console.WriteLine("Received STOP signal.");
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
	/// Saves out progress to a file.
	/// </summary>
	public void SaveOut()
	{
		string succeededFile = Path.Combine(ProjectDataDirectory, "succeeded.json");
		File.WriteAllText(succeededFile, JsonConvert.SerializeObject(s_succeeded.ToArray()));
	}

	/// <summary>
	/// Returns the title of the uploaded page for the specified metadata.
	/// </summary>
	protected abstract string GetTitle(string key, Dictionary<string, string> metadata);

	/// <summary>
	/// Prepares the image for upload and returns the path to the file to upload.
	/// </summary>
	protected abstract string GetUploadImagePath(string key, Dictionary<string, string> metadata);

	/// <summary>
	/// Builds the wiki page for the object with the specified metadata.
	/// </summary>
	protected abstract string BuildPage(string key, Dictionary<string, string> metadata);

	#region Parse Helpers

	/// <summary>
	/// Returns the appropriate category check tag.
	/// </summary>
	protected static string GetCheckCategoriesTag(int categoryCount)
	{
		string dmy = "day=" + DateTime.Now.Day + "|month="
			+ CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month)
			+ "|year=" + DateTime.Now.Year;
		if (categoryCount <= 0)
		{
			return "{{uncategorized|" + dmy + "}}";
		}
		else
		{
			return "{{check categories|" + dmy + "|ncats=" + categoryCount + "}}";
		}
	}

	/// <summary>
	/// Get a string that should be used for the file's 'author' field.
	/// </summary>
	protected string GetAuthor(string name, string lang, out Creator creator)
	{
		//TODO: support multiple creators
		creator = null;

		string finalResult = "";
		foreach (string author in ParseAuthor(name))
		{
			if (string.Equals(author, "Anonymous", StringComparison.InvariantCultureIgnoreCase))
			{
				return "{{anonymous}}";
			}
			//Check for a Creator template
			else if (CreatorUtility.TryGetCreator(author, out creator))
			{
				creator.Usage++;
				if (!string.IsNullOrEmpty(creator.Author))
				{
					finalResult += creator.Author;
					continue;
				}
			}

			// if we get here, there is not yet a mapping for this creator
			if (m_config.allowFailedCreators)
				finalResult += "{{" + lang + "|" + author + "}}";
			else
				throw new UWashException("unrecognized creator|" + author);
		}

		return finalResult;
	}

	private static IEnumerable<string> ParseAuthor(string name)
	{
		string[] authors = name.Split(StringUtility.Pipe);
		for (int c = 0; c < authors.Length; c++)
		{
			authors[c] = CleanPersonName(authors[c]);

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

	private static char[] s_dobTrim = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', ' ', '?', ',' };

	/// <summary>
	/// Cleans DOB information from a name string.
	/// </summary>
	protected static string CleanPersonName(string name)
	{
		//Remove trailing DOB/lifetime
		//HACK: be more explicit with regex
		return name.TrimEnd(s_dobTrim);
	}

	/// <summary>
	/// Parses an object's physical description (dimensions, medium).
	/// </summary>
	protected void ParsePhysicalDescription(string raw, out string medium, out Dimensions dimensions)
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

	/// <summary>
	/// Parses a month string to an index (one-based).
	/// </summary>
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

	#endregion
}
