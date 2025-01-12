﻿using MediaWiki;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace UWash
{
	public class MohaiCreditFix : BatchTaskKeyed<int>
	{
		protected Api Api = new Api(new Uri("https://commons.wikimedia.org/"));

		public MohaiCreditFix(string key)
			: base(key)
		{
			Api.AutoLogIn();
		}

		public override void Execute()
		{
			bool skipping = true;

			foreach (Article article in Api.GetCategoryEntries("Category:Images from the Museum of History and Industry", CMType.file))
			{
				if (skipping)
				{
					if (article.title.StartsWith("File:Jitney"))
					{
						skipping = false;
					}
					continue;
				}

				Console.WriteLine(article.title);

				string accessionIndicator = "(MOHAI ";
				int accessionStart = article.title.LastIndexOf(accessionIndicator) + accessionIndicator.Length;
				int accessionPostEnd = article.title.IndexOf(')', accessionStart);
				string accessionNumberString = article.title.Substring(accessionStart, accessionPostEnd - accessionStart);
				int accessionNumber = int.Parse(accessionNumberString);

				string trashFile = GetMetadataTrashFilename(accessionNumber);
				Dictionary<string, string> metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(trashFile, Encoding.UTF8));

				string creditLineValue;
				if (metadata.TryGetValue("Credit Line", out creditLineValue)
					&& (creditLineValue.Contains("[image number") || creditLineValue.Contains("[ID number")))
				{
					Article popArticle = Api.GetPage(article);
					string articleText = popArticle.revisions[0].text;

					string imageNumber;
					if (metadata.TryGetValue("Image Number", out imageNumber))
					{

					}
					else if (metadata.TryGetValue("ID Number", out imageNumber))
					{

					}
					else
					{
						Debug.Assert(false);
					}

					imageNumber = imageNumber.Trim();

					articleText = articleText.Replace("[image number}}", imageNumber + "}}");
					articleText = articleText.Replace("[ID number}}", imageNumber + "}}");
					if (popArticle.revisions[0].text != articleText)
					{
						popArticle.revisions[0].text = articleText;
						Debug.Assert(Api.EditPage(popArticle, "update Credit Line"));
					}
				}
			}
		}
	}
}