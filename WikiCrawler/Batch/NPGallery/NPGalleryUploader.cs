using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WikiCrawler;

namespace NPGallery
{
	public class NPGalleryUploader : BatchUploader
	{
		private HashSet<string> m_usedKeys = new HashSet<string>()
		{
			"Photographer",
			"Create Date",
			"Location",
			"Categories",
			"Description",

			// Unused
			"Asset ID",
			"Related Collections",
			"Time Submitted",
			"Last Editted",
			"Original File Name",
			"Resource Format",
			"File Size (bytes)",
			"Rating",
			"Original NPS Focus Uploader"
		};

		public NPGalleryUploader(string key)
			: base(key)
		{

		}

		private const string ImageUriFormat = "https://npgallery.nps.gov/GetAsset/{0}/original.jpg";

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			Creator creator;
			string photographer = metadata["Photographer"].Trim();
			if (photographer.StartsWith("<text>"))
			{
				photographer = photographer.Substring("<text>".Length);
			}
			if (photographer.EndsWith("</text>"))
			{
				photographer = photographer.Substring(0, photographer.Length - "</text>".Length);
			}
			photographer = GetAuthor(photographer, "en", out creator);
			if (creator == null)
			{
				throw new UWashException("No creator");
			}

			DateParseMetadata dateMetadata;
			string date = DateUtility.ParseDate(metadata["Create Date"], out dateMetadata);

			List<string> categories = new List<string>();
			categories.Add(m_config.checkCategory);

			//TODO:
			string location = metadata["Location"];
			IEnumerable<string> locationCats = CategoryTranslation.TranslateLocationCategory(metadata["Location"]);
			string locationCat = locationCats == null ? "" : locationCats.FirstOrDefault();
			if (!string.IsNullOrEmpty(locationCat))
			{
				location = locationCat.Substring("Category:".Length);

				if (dateMetadata.PreciseYear != 0)
				{
					string yearLocCat = "Category:" + dateMetadata.PreciseYear.ToString() + " in " + location;
					Wikimedia.Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
					if (existingYearLocCat != null)
					{
						categories.Add(existingYearLocCat.title);
					}
					else
					{
						categories.Add(locationCat);
					}
				}
				else
				{
					categories.Add(locationCat);
				}
			}

			string categoriesData;
			if (metadata.TryGetValue("Categories", out categoriesData))
			{
				foreach (string subject in categoriesData.Split(','))
				{
					IEnumerable<string> mappedCats = CategoryTranslation.TranslateTagCategory(subject);
					if (mappedCats != null)
					{
						categories.AddRange(mappedCats);
					}
				}
			}

			if (creator != null && !string.IsNullOrEmpty(creator.Category))
			{
				categories.Add(creator.Category);
			}

			string licenseTag = GetLicenseTag(photographer, creator, dateMetadata.LatestYear, m_config.defaultPubCountry);

			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new LicenseException();
			}

			string otherFields = "";
			if (!string.IsNullOrEmpty(metadata["OriginalNegativeHeld"]))
			{
				otherFields += "\n{{Information field|name=Original Negative Held|value=" + metadata["OriginalNegativeHeld"] + "}}";
			}

			string page = GetCheckCategoriesTag(categories.Count) + "\n"
				+ "=={{int:filedesc}}==\n"
				+ "{{Photograph\n"
				+ "|photographer=" + photographer + "\n"
				+ "|title={{en|" + metadata["Title"] + "}}\n"
				+ "|description=\n"
				+ "{{en|" + metadata["Description"].Trim('"') + "}}\n"
				+ "|depicted place=" + location + "\n"
				+ "|date=" + date + "\n"
				+ "|accession number={{NPGallery-accession|" + key + "}}\n"
				+ "|source=" + m_config.sourceTemplate + "\n"
				+ "|permission=" + licenseTag + "\n"
				+ "|other_versions=\n"
				+ "|other_fields=" + otherFields + "\n"
				+ "}}\n"
				+ "\n";

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			foreach (string cat in categories)
			{
				page += "[[" + cat + "]]\n";
			}

			return page;
		}

		protected override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			return new Uri(string.Format(ImageUriFormat, key));
		}

		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			return HttpUtility.HtmlDecode(metadata["Title"]);
		}
	}
}
