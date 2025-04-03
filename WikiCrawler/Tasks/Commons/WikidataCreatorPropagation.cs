using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tasks.Commons
{
	/// <summary>
	/// Propagates data from Commons Creator pages to Wikidata.
	/// </summary>
	public class WikidataCreatorPropagation : BaseTask
	{
		public override void Execute()
		{
			List<string> creators = new List<string>();

			using (StreamReader reader = new StreamReader(new FileStream("authority_in.txt", FileMode.Open), Encoding.Default))
			{
				while (!reader.EndOfStream)
				{
					creators.Add(reader.ReadLine());
				}
			}

			foreach (string creatorPage in creators)
			{
				Console.WriteLine(creatorPage);

				// not creator? just output authority
				PageTitle creatorTitle = CreatorUtility.GetCreatorTemplate(creatorPage);
				if (creatorTitle.IsEmpty)
				{
					Console.WriteLine("FATAL: not a creator");
					continue;
				}

				Article creatorArticle = GlobalAPIs.Commons.GetPage(creatorPage);

				if (creatorArticle == null || creatorArticle.missing)
				{
					Console.WriteLine("FATAL: failed to get article");
					continue;
				}

				//read wikidata link
				string articleText = creatorArticle.revisions[0].text;
				List<string> articleLines = new List<string>();
				articleLines.AddRange(articleText.Split('\n'));

				string wikidataId = WikiUtils.GetTemplateParameter("wikidata", articleText);

				//got it?
				if (!string.IsNullOrEmpty(wikidataId) && wikidataId[0] == 'Q')
				{
					Entity entity = GlobalAPIs.Wikidata.GetEntity(wikidataId);
					if (entity != null && !entity.missing)
					{
						string homecat = WikiUtils.GetTemplateParameter("homecat", articleText);
						if (!string.IsNullOrEmpty(homecat) && !entity.HasClaim("P373"))
						{
							//propagate homecat
							GlobalAPIs.Wikidata.CreateEntityClaim(entity, "P373", homecat, "(BOT) propagating Commons category from Creator", true);
						}

						if (!entity.HasClaim("P1472"))
						{
							//propagate creator
							GlobalAPIs.Wikidata.CreateEntityClaim(entity, "P1472", creatorTitle.Name, "(BOT) propagating Commons Creator", true);
						}

						// populate creator authority
						string authority = Wikidata.GetAuthorityControlTemplate(entity, "bare=1", null);
						if (!string.IsNullOrEmpty(authority))
						{
							// look for existing authority
							int authorityLine = -1;
							bool existingAuthority = false;
							for (int line = 0; line < articleLines.Count && authorityLine < 0; line++)
							{
								string[] split = articleLines[line].Split(new char[] { '|' }, 2);
								if (split.Length == 2)
								{
									if (split[0].Trim().Equals("authority", StringComparison.InvariantCultureIgnoreCase))
									{
										authorityLine = line;
										string authParam = split[1].Trim();
										if (authParam.StartsWith("<!--") && authParam.EndsWith("-->"))
										{
											// it's a comment - remove it
											articleLines[line] = split[0] + "|";
										}
										else
										{
											existingAuthority = true;
										}
									}
								}
							}

							if (existingAuthority)
							{
								Console.WriteLine("FATAL: already has authority");
							}
							else if (authorityLine >= 0)
							{
								// insert content into authority param
								articleLines[authorityLine] += " " + authority;
								creatorArticle.revisions[0].text = string.Join("\n", articleLines.ToArray());
								GlobalAPIs.Commons.EditPage(creatorArticle, "(BOT) propagate authorities from Wikidata");
							}
							else
							{
								// no line available - make one
								bool success = false;
								for (int line = articleLines.Count - 1; line >= 0 ; line--)
								{
									if (articleLines[line].TrimStart().StartsWith("|"))
									{
										// add it after this
										//HACK:
										articleLines.Insert(line + 1, " | Authority = " + authority);
										success = true;
										break;
									}
								}

								if (success)
								{
									creatorArticle.revisions[0].text = string.Join("\n", articleLines.ToArray());
									GlobalAPIs.Commons.EditPage(creatorArticle, "(BOT) propagate authorities from Wikidata");
								}
								else
								{
									Console.WriteLine("FATAL: inserting param failed");
								}
							}
						}
						else
						{
							Console.WriteLine("FATAL: no authority available from Wikidata");
						}
					}
					else
					{
						Console.WriteLine("FATAL: couldn't find wikidata '" + wikidataId + "'");
					}
				}
				else
				{
					Console.WriteLine("FATAL: no wikidata id");
				}
			}
		}
	}
}
