using System;
using System.Collections.Generic;
using System.Linq;
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

			HashSet<string> categories = new HashSet<string>();
			categories.Add(m_config.checkCategory);

			string location = metadata["Location"];
			IEnumerable<string> locationCats = CategoryTranslation.TranslateLocationCategory(metadata["Location"]);
			string locationCat = locationCats == null ? "" : locationCats.FirstOrDefault();
			if (!string.IsNullOrEmpty(locationCat))
			{
				location = locationCat.Substring("Category:".Length);

				if (dateMetadata.IsPrecise)
				{
					string yearLocCat = "Category:" + dateMetadata.PreciseYear.ToString() + " in " + location;
					MediaWiki.Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
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
				IEnumerable<string> mappedCats = CategoryTranslation.TranslateTagCategory(subject);
				if (mappedCats != null)
				{
					categories.AddRange(mappedCats);
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
				+ "{{en|\n"
				+ "<p>" + metadata["Notes"].Trim('"') + "</p>\n"
				+ "*Subject: " + metadata["Subject"] + "\n"
				+ "}}\n"
				+ "|depicted place=" + location + "\n"
				+ "|date=" + date + "\n"
				+ "|institution={{Institution:Digital South Asia Library, Chicago}}\n"
				+ "|accession number={{DSAL-" + m_projectKey.ToUpper() + "-accession|" + key + "}}\n"
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

		public override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			return new Uri(metadata["ImageUrl"]);
		}

		public override string GetTitle(string key, Dictionary<string, string> metadata)
		{
			return metadata["Title"] + " (" + m_config.filenameSuffix + " " + key.Substring(key.Length - 4, 4) + ")";
		}
	}
}
