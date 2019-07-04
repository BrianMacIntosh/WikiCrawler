using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Wikimedia;

namespace OEC
{
	public class OecUploader : BatchUploader
	{
		private Dictionary<string, string> CountryCodeToName = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

		public OecUploader(string key)
			: base(key)
		{
			foreach (string line in File.ReadAllLines(Path.Combine(ProjectDataDirectory, "country_codes.txt"), Encoding.UTF8))
			{
				if (!string.IsNullOrEmpty(line))
				{
					string[] split = line.Split(new char[] { ' ' }, 2);
					CountryCodeToName[split[0]] = split[1].Trim();
				}
			}
		}

		protected override Dictionary<string, string> ParseMetadata(string fileContents)
		{
			return new Dictionary<string, string>();
		}

		public override void CacheImage(string key, Dictionary<string, string> metadata)
		{
			//base.CacheImage(key, metadata);
		}

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			// process svg
			string rawFile = GetMetadataCacheFilename(key);
			XDocument svg = XDocument.Load(rawFile);

			Dictionary<string, XElement> keyElements = new Dictionary<string, XElement>();

			foreach (XElement element in ElementsFlat(svg.Root).ToList())
			{
				// replace fonts
				XAttribute fontFamilyA = element.Attribute("font-family");
				if (fontFamilyA != null)
				{
					string fontFamily = fontFamilyA.Value;
					if (fontFamily == "HelveticaNeue-CondensedBold")
					{
						fontFamilyA.Value = "Ubuntu Condensed";

						XAttribute fontWeightA = element.Attribute("font-weight");
						if (fontWeightA != null)
						{
							fontWeightA.Remove();
						}
					}
				}

				// center numbers
				XAttribute xA = element.Attribute("x");
				XAttribute dxA = element.Attribute("dx");
				if (xA != null && dxA != null)
				{
					decimal x = decimal.Parse(xA.Value.TrimEnd("px"));
					decimal dx = decimal.Parse(dxA.Value.TrimEnd("px"));
					if (x == -dx)
					{
						xA.Value = "0px";
						dxA.Value = "0px";
					}
				}

				// increase stroke
				if (element.Name.LocalName == "rect")
				{
					XAttribute classA = element.Attribute("class");
					if (classA != null && classA.Value == "d3plus_data")
					{
						XAttribute styleA = element.Attribute("style");
						if (styleA != null)
						{
							string style = styleA.Value;
							string strokeWidthTag = "stroke-width: ";
							int strokeWidthIndex = style.IndexOf(strokeWidthTag);
							if (strokeWidthIndex >= 0)
							{
								int valueStartIndex = strokeWidthIndex + strokeWidthTag.Length;
								int endIndex = style.IndexOf(';', valueStartIndex);
								string stroke = style.Substring(valueStartIndex, endIndex - valueStartIndex);
								if (int.Parse(stroke) == 1)
								{
									style = style.Substring(0, valueStartIndex) + "2" + style.Substring(endIndex);
									styleA.Value = style;
								}
							}
						}
					}
				}

				// inline key images
				if (element.Name.LocalName == "rect")
				{
					XAttribute fillA = element.Attribute("fill");
					if (fillA != null && fillA.Value.StartsWith("url(#staticimgiconshshs_"))
					{
						keyElements.Add(fillA.Value.Substring("url(#".Length).TrimEnd(')'), element);
					}
				}
				if (element.Name.LocalName == "pattern")
				{
					XAttribute idA = element.Attribute("id");
					XElement keyElement;
					if (idA != null && keyElements.TryGetValue(idA.Value, out keyElement))
					{
						XElement targetParent = keyElement.Parent;
						keyElement.Remove();
						foreach (XElement sourceChild in element.Nodes().ToList())
						{
							sourceChild.Remove();
							targetParent.Add(sourceChild);
						}
					}
					element.Remove();
				}
			}

			string outputFilename = GetImageCacheFilename(key, metadata);
			svg.Save(outputFilename);

			string countryCode, country, year;
			ParseKey(key, out countryCode, out country, out year);

			// assemble page
			string page = "=={{int:filedesc}}==\n"
				+ "{{Information\n"
				+ "|description={{en|1=A treemap representing the exports of " + country + " in " + year.ToString() + ".}}\n"
				+ "|date=" + System.DateTime.Now.ToString("yyyy-MM-dd") + "\n"
				+ "|source=Economic Complexity Observatory, MIT Media Lab and the Center for International Development at Harvard University. http://atlas.media.mit.edu/\n"
				+ "|author=Alexander Simoes, Cesar Hidalgo, et. al. See [https://atlas.media.mit.edu/en/resources/about/ OEC - About].\n"
				+ "|permission=\n"
				+ "|other_versions=\n"
				+ "|other_fields=\n"
				+ "}}\n"
				+ "{{OEC-Treemap-Index2}}\n"
				+ "\n"
				+ "=={{int:license-header}}==\n"
				+ "{{cc-by-sa-3.0}}\n"
				+ "\n"
				+ "[[Category:Treemaps on exports]]\n"
				+ "[[Category:" + country + "]]\n" //TODO: validate?
				+ "[[Category:Images from the Observatory of Economic Complexity]]";

			return page;
		}

		protected override void PostUpload(string key, Dictionary<string, string> metadata, Article article)
		{
			base.PostUpload(key, metadata, article);

			string countryCode, country, year;
			ParseKey(key, out countryCode, out country, out year);

			// supercede the old image
			Article oldImage = Api.GetPage("File:" + country + " Export Treemap.jpg");
			if (oldImage == null || oldImage.missing)
			{
				oldImage = Api.GetPage("File:" + country + " Export Treemap.png");
			}
			if (oldImage == null || oldImage.missing)
			{
				WindowsUtility.FlashWindowEx(Process.GetCurrentProcess().MainWindowHandle);
				Console.Write(country + " Old Page>");
				string temp = Console.ReadLine();
				if (!string.IsNullOrEmpty(temp))
				{
					oldImage = Api.GetPage(temp);
				}
			}
			if (oldImage == null || oldImage.missing)
			{
				return;
			}

			int startIndex = oldImage.revisions[0].text.IndexOf("=={{int:filedesc}}==");

			oldImage.revisions[0].text =
				oldImage.revisions[0].text.Substring(0, startIndex)
				+ "{{Superseded|" + article.title + "}}\n"
				+ oldImage.revisions[0].text.Substring(startIndex);

			Api.SetPage(oldImage, "Superseded by [[:" + article.title + "]]", false, true);
		}

		private IEnumerable<XElement> ElementsFlat(XElement element)
		{
			foreach (XElement element2 in element.Elements())
			{
				yield return element2;
				foreach (XElement element3 in ElementsFlat(element2))
				{
					yield return element3;
				}
			}
		}

		protected override string GetImageCacheFilename(string key, Dictionary<string, string> metadata)
		{
			return Path.ChangeExtension(base.GetImageCacheFilename(key, metadata), ".svg");
		}

		protected override string GetMetadataCacheFilename(string key)
		{
			return Path.ChangeExtension(base.GetMetadataCacheFilename(key), ".svg");
		}

		protected override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			return new Uri("");
		}

		private void ParseKey(string key, out string countryCode, out string country, out string year)
		{
			countryCode = key.Substring("en_visualize_explore_tree_map_hs92_export_".Length, 3);
			year = key.Substring(key.Length - 4);
			if (!CountryCodeToName.TryGetValue(countryCode, out country))
			{
				WindowsUtility.FlashWindowEx(Process.GetCurrentProcess().MainWindowHandle);
				Console.Write(countryCode);
				Console.Write(">");
				country = Console.ReadLine();
				CountryCodeToName[countryCode] = country;
			}
		}

		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			string countryCode, country, year;
			ParseKey(key, out countryCode, out country, out year);
			return country + " Exports Treemap " + year.ToString();
		}
	}
}
