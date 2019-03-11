using System;
using System.Collections.Generic;
using WikiCrawler;

namespace Dsal
{
	public class DsalUploader : BatchUploader
	{
		public DsalUploader(string key)
			: base(key)
		{
		}

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			Creator creator;
			string photographer = GetAuthor(metadata["Photographer"], "en", out creator);
			if (creator == null)
			{
				throw new UWashException("No creator");
			}

			DateParseMetadata dateMetadata;
			string date = DateUtility.ParseDate(metadata["Date"], out dateMetadata);

			List<string> categories = new List<string>();
			categories.Add(m_config.checkCategory);

			string location = metadata["Location"];
			string locationCat = CategoryTranslation.TranslateLocationCategory(metadata["Location"]);
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

			foreach (string subject in metadata["Subject"].Trim('"').Split(','))
			{
				string mappedCat = CategoryTranslation.TranslateTagCategory(subject);
				if (!string.IsNullOrEmpty(mappedCat))
				{
					categories.Add(mappedCat);
				}
			}

			if (creator != null && !string.IsNullOrEmpty(creator.Category))
			{
				categories.Add(creator.Category);
			}

			string licenseTag = GetLicenseTag(photographer, creator, dateMetadata.LatestYear, m_config.defaultPubCountry);

			if (string.IsNullOrEmpty(licenseTag))
			{
				throw new UWashException("No license?");
			}

			string page = GetCheckCategoriesTag(categories.Count) + "\n"
				+ "=={{int:filedesc}}==\n"
				+ "{{Photograph\n"
				+ "|photographer=" + photographer + "\n"
				+ "|title={{en|" + metadata["Title"] + "}}\n"
				+ "|description=\n"
				+ "{{en|\n"
				+ "<p>" + metadata["Notes"].Trim('"') + "</p>\n"
				+ "*Subject: " + metadata["Subject"] + "\n"
				+ "}}\n"
				+ "|depicted place=" + location + "\n"
				+ "|date=" + date + "\n"
				+ "|institution={{Institution:Digital South Asia Library, Chicago}}\n"
				+ "|accession number={{DSAL-BOND-accession|" + key + "}}\n"
				+ "|source={{DSAL-BOND-source}}\n"
				+ "|permission=" + licenseTag + "\n"
				+ "|other_versions=\n"
				+ "|other_fields=\n"
				+ "{{Information field|name=Original Negative Held|value=" + metadata["OriginalNegativeHeld"] + "}}\n"
				+ "}}\n"
				+ "\n";

			CategoryTranslation.CategoryTree.RemoveLessSpecific(categories);

			foreach (string cat in categories)
			{
				page += "[[" + cat + "]]\n";
			}

			return page;
		}

		protected override Uri GetImageUri(string key)
		{
			return new Uri("http://dsal.uchicago.edu/images/bond/images/large/" + key + ".jpg");
		}

		protected override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			return metadata["Title"] + " (" + m_config.filenameSuffix + " " + key.Substring(key.Length - 4, 4) + ")";
		}

		protected override string GetUploadImagePath(string key, Dictionary<string, string> metadata)
		{
			return GetImageCacheFilename(key);
		}
	}
}
