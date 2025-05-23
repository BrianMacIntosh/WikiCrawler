using MediaWiki;
using System;
using System.Collections.Generic;
using System.Linq;
using WikiCrawler;

namespace Dsal
{
	public class DsalUploader : BatchUploader<string>
	{
		public DsalUploader(string key)
			: base(key)
		{
		}

		protected override string BuildPage(string key, Dictionary<string, string> metadata)
		{
			List<Creator> creators = new List<Creator>();
			string photographer = GetAuthor(metadata["Photographer"], "en", ref creators);
			if (creators.Count == 0)
			{
				throw new UWashException("No creator");
			}

			DateParseMetadata dateMetadata;
			string date = DateUtility.ParseDate(metadata["Date"], out dateMetadata);

			HashSet<PageTitle> categories = new HashSet<PageTitle>();
			categories.Add(PageTitle.Parse(m_config.checkCategory));

			string location = metadata["Location"];
			IEnumerable<PageTitle> locationCats = CategoryTranslation.TranslateLocationCategory(metadata["Location"]);
			PageTitle locationCat = locationCats == null ? PageTitle.Empty : locationCats.FirstOrDefault();
			if (!locationCat.IsEmpty)
			{
				location = locationCat.Name;

				if (dateMetadata.IsPrecise)
				{
					PageTitle yearLocCat = new PageTitle(PageTitle.NS_Category, dateMetadata.PreciseYear.ToString() + " in " + location);
					Article existingYearLocCat = CategoryTranslation.TryFetchCategory(Api, yearLocCat);
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
				IEnumerable<PageTitle> mappedCats = CategoryTranslation.TranslateTagCategory(subject);
				if (mappedCats != null)
				{
					categories.AddRange(mappedCats);
				}
			}

			foreach (Creator creator in creators)
			{
				if (!creator.Category.IsEmpty)
				{
					categories.Add(creator.Category);
				}
			}

			string licenseTag = GetLicenseTag(photographer, creators, dateMetadata.LatestYear, m_config.defaultPubCountry);

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

			foreach (PageTitle cat in categories)
			{
				page += "[[" + cat + "]]\n";
			}

			return page;
		}

		public override Uri GetImageUri(string key, Dictionary<string, string> metadata)
		{
			return new Uri(metadata["ImageUrl"]);
		}

		public override PageTitle GetTitle(string key, Dictionary<string, string> metadata)
		{
			return new PageTitle(
				PageTitle.NS_File,
				metadata["Title"] + " (" + m_config.filenameSuffix + " " + key.Substring(key.Length - 4, 4) + ")");
		}
	}
}
