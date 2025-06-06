﻿using MediaWiki;
using System;
using System.IO;
using System.Linq;
using WikiCrawler;

public static class CommonsUtility
{
	private static readonly string[] s_languageTemplates;

	static CommonsUtility()
	{
		s_languageTemplates = File.ReadAllLines(Path.Combine(Configuration.DataDirectory, "language-templates.txt"));
	}

	public static string GetLanguageTemplate(string parse)
	{
		switch (parse.ToLower())
		{
			case "English":
				return "en";
			case "French":
				return "fr";
			default:
				throw new UWashException("Unrecognized language '" + parse + "'.");
		}
	}

	/// <summary>
	/// Returns true if the specified template name is a language template.
	/// </summary>
	public static bool IsLanguageTemplate(string templateName)
	{
		return s_languageTemplates.Contains(templateName, PageNameComparer.Instance);
	}

	/// <summary>
	/// Ensures that the category tree for the {{taken on}} template with the specified date is created.
	/// </summary>
	public static void EnsureTakenOnCategories(Api api, string date)
	{
		if (DateUtility.IsExactDateModern(date))
		{
			Console.WriteLine("Checking for Taken On categories...");
			string[] dateSplit = date.Split('-');

			int year = int.Parse(dateSplit[0]);
			if (year < 1000)
			{
				// suspicious
				return;
			}

			// cat name day/month should be padded
			dateSplit[1] = int.Parse(dateSplit[1]).ToString("00");
			dateSplit[2] = int.Parse(dateSplit[2]).ToString("00");
			date = string.Join("-", dateSplit);

			PageTitle takenOnCatTitle = new PageTitle(PageTitle.NS_Category, "Photographs taken on " + date);
			Article takenOnCatArticle = api.GetPage(takenOnCatTitle);
			if (takenOnCatArticle == null || takenOnCatArticle.missing)
			{
				takenOnCatArticle = new Article(takenOnCatTitle);
				takenOnCatArticle.revisions = new Revision[1];
				takenOnCatArticle.revisions[0] = new Revision() { text = "{{Photographs taken on navbox|" + date.Replace('-', '|') + "}}" };
				api.CreatePage(takenOnCatArticle, "creating new category");
			}
			PageTitle dateCatTitle = new PageTitle(PageTitle.NS_Category, date);
			Article dateCatArticle = api.GetPage(dateCatTitle);
			if (dateCatArticle == null || dateCatArticle.missing)
			{
				dateCatArticle = new Article(dateCatTitle);
				dateCatArticle.revisions = new Revision[1];
				dateCatArticle.revisions[0] = new Revision() { text = "{{Date navbox|" + date.Replace('-', '|') + "}}" };
				api.CreatePage(dateCatArticle, "creating new category");
			}
			PageTitle monthCatTitle = new PageTitle(PageTitle.NS_Category, DateUtility.GetMonthName(int.Parse(dateSplit[1])) + " " + dateSplit[0]);
			Article monthCatArticle = api.GetPage(monthCatTitle);
			if (monthCatArticle == null || monthCatArticle.missing)
			{
				monthCatArticle = new Article(monthCatTitle);
				monthCatArticle.revisions = new Revision[1];
				monthCatArticle.revisions[0] = new Revision()
				{
					text = "{{monthbyyear|" + dateSplit[0].Substring(0, 3) + "|" + dateSplit[0].Substring(3, 1) + "|" + dateSplit[1] + "}}"
				};
				api.CreatePage(monthCatArticle, "creating new category");
			}
			PageTitle monthPhotosCatTitle = new PageTitle(PageTitle.NS_Category, DateUtility.GetMonthName(int.Parse(dateSplit[1])) + " " + dateSplit[0] + " photographs");
			Article monthPhotosCatArticle = api.GetPage(monthPhotosCatTitle);
			if (monthPhotosCatArticle == null || monthPhotosCatArticle.missing)
			{
				monthPhotosCatArticle = new Article(monthPhotosCatTitle);
				monthPhotosCatArticle.revisions = new Revision[1];
				monthPhotosCatArticle.revisions[0] = new Revision()
				{
					text = "{{PhotographsYearByMonth|" + dateSplit[0] + "|" + dateSplit[1] + "}}"
				};
				api.CreatePage(monthPhotosCatArticle, "creating new category");
			}
		}
	}
}
