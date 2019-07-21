using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	class Tropenmuseum
	{
		private static MediaWiki.Api Api = new MediaWiki.Api(new Uri("https://commons.wikimedia.org/"));

		public void Do()
		{
			string dataFile = "tropendata.csv";

			using (StreamReader reader = new StreamReader(new FileStream(dataFile, FileMode.Open)))
			{
				string[] columnHead = reader.ReadLine().Split(',');
				while (!reader.EndOfStream)
				{
					string[] props = reader.ReadLine().Split(',');
					Dictionary<string, string> data = new Dictionary<string, string>();
					for (int c = 0; c < columnHead.Length; c++)
					{
						Console.WriteLine(props[0]);
						data[columnHead[c]] = props[c];
						DoUpload(Api, data);
					}
				}
			}
		}

		public void DoUpload(MediaWiki.Api api, Dictionary<string, string> data)
		{
			string title = string.Format(
				"COLLECTIE STICHTING NATIONAAL MUSEUM VAN WERELDCULTUREN {0} {1}.jpg",
				!string.IsNullOrEmpty(data["Title EN"]) ? data["Title EN"] : data["Title NL"],
				data["ObjectNumber"]);

			string titleContent = "";
			if (!string.IsNullOrEmpty(data["Title NL"]))
			{
				titleContent += "{{nl|" + data["Title NL"] + "}}";
			}
			if (!string.IsNullOrEmpty(data["Title EN"]))
			{
				titleContent += "{{en|" + data["Title EN"] + "}}";
			}

			//TODO:
			string imagePath = "";

			string materialContent = "";
			if (!string.IsNullOrEmpty(data["Materiaal - techniek - medium (nl)"]))
			{
				materialContent += "{{nl|" + data["Materiaal - techniek - medium (nl)"] + "}}";
			}
			if (!string.IsNullOrEmpty(data["Title EN"]))
			{
				materialContent += "{{en|" + data["Material - technique - medium (en)"] + "}}";
			}
			
			//TODO:
			//Description;
			//Type of object;
			//Religion;
			//Culture;
			//Indigenous Name;
			//Onderwerp (nl);
			//Subject (en);

			string text = @"=={{int:filedesc}}==
{{Photograph
 |photographer = " + data["Photographer"] + @"
 |title = " + titleContent + @"
 |description = '''Cultuur:''' Antilliaans, Hindoestaans, Nederlands, Surinaams Javaans {{nl|'''Onderwerp:''' bestuurlijke gebeurtenis, groep, museum, portret}}{{en|'''Subject:''' museum, portrait}}
 |depicted people = " + data["Related person(s)"] + @"
 |depicted place = " + data["Related location(s)"] + @"
 |date = " + ParseDate(data["Date"]) + @"
 |medium = " + materialContent + @"
 |dimensions = " + ParseDimensions(data["Dimensions"]) + @"
 |institution = " + data["Related institution(s)"] + @"
 |department =
 |references =
 |object history =
 |exhibition history =
 |credit line = " + data["Credits"] + @"
 |inscriptions =
 |notes =
 |accession number = " + data["ObjectNumber"] + @"
 |source = {{KIT-ccid|" + data["ObjectID"] + @"}}{{Expedition Wikipedia}}
 |permission = " + data["License"] + @"
 |other_versions =
}}

";

			if (!string.IsNullOrEmpty(data["Commons category 1"]))
			{
				text += "[[Category:" + data["Commons category 1"] + "]]";
			}
			if (!string.IsNullOrEmpty(data["Commons category 2"]))
			{
				text += "[[Category:" + data["Commons category 2"] + "]]";
			}

			MediaWiki.Article art = new MediaWiki.Article();
			art.title = title;
			art.revisions = new MediaWiki.Revision[1];
			art.revisions[0].text = text;

			Api.UploadFromLocal(art, imagePath, "(BOT) batch image upload (see [[Commons:Batch uploading/Tropenmuseum Expeditions]])", true);
		}

		private static string ParseDate(string date)
		{
			if (date.EndsWith("T00:00:00Z"))
			{
				date = date.Substring(0, date.Length - "T00:00:00Z".Length);
			}
			return DateUtility.ParseDate(date);
		}

		private static string ParseDimensions(string dimensions)
		{
			string[] split = dimensions.Split('(');
			string realDimensions = split[0].Trim();

			string[] realsplit = realDimensions.Split('x');
			string units;
			string w = realsplit[0].Trim();
			string h = RemoveUnits(realsplit[0].Trim(), out units);

			float wi = float.Parse(w, new CultureInfo("nl-NL"));
			float hi = float.Parse(h, new CultureInfo("nl-NL"));

			return "{{size|unit=" + units + "|width=" + wi + "|height=" + hi + "}}";
		}

		private static string RemoveUnits(string dimension, out string units)
		{
			for (int i = dimension.Length - 1; i >= 0; i--)
			{
				if (char.IsNumber(dimension[i]))
				{
					units = dimension.Substring(i + 1);
					return dimension.Substring(0, i + 1);
				}
			}
			units = "";
			return dimension;
		}
	}
}
