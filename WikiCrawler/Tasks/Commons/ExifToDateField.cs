using MediaWiki;
using System;
using System.IO;
using System.Text;
using WikiCrawler;

namespace Tasks
{
	public class ExifToDateField : BaseTask
	{
		public override void Execute()
		{
			using (StreamWriter logWriter = new StreamWriter(
				new FileStream(Path.Combine(Configuration.DataDirectory, "exiftodate.txt"), FileMode.Append, FileAccess.Write), Encoding.UTF8))
			{
				DoHelper(logWriter);
			}
		}

		private static void DoHelper(StreamWriter logWriter)
		{
			Api commonsApi = new Api(new Uri("https://commons.wikimedia.org"));

			Console.WriteLine("Logging in...");
			commonsApi.AutoLogIn();

			int maxEdits = int.MaxValue;

			foreach (Article file in commonsApi.GetCategoryEntries("Category:Photographs by Jiří Bubeníček", CMType.file))
			{
				if (maxEdits <= 0)
				{
					break;
				}

				Console.WriteLine(file.title);

				Article fileGot = commonsApi.GetPage(file, prop: "info|revisions|imageinfo", iiprop: "metadata|url");
				string pageText = fileGot.revisions[0].text;

				int infoStart = pageText.IndexOf("{{Information");
				if (infoStart >= 0)
				{
					int infoEnd = WikiUtils.GetTemplateEnd(pageText, infoStart);
					if (infoEnd >= 0)
					{
						string informationText = pageText.Substring(infoStart + 2, infoEnd - infoStart - 2);

						int dateLocation;
						string date = WikiUtils.GetTemplateParameter("Date", informationText, out dateLocation);
						dateLocation += infoStart + 2;
						if (string.IsNullOrWhiteSpace(date))
						{
							if (fileGot.imageinfo[0].metadata.TryGetValue("DateTimeOriginal", out string exifDate))
							{
								string[] dateTime = exifDate.Split(' ');
								if (dateTime.Length == 2 && dateTime[0].Length == 10)
								{
									string dateTag = "{{According to Exif data|" + dateTime[0].Replace(':', '-') + "}}";
									pageText = pageText.Substring(0, dateLocation) + dateTag + pageText.Substring(dateLocation + date.Length);
									fileGot.revisions[0].text = pageText;
									commonsApi.SetPage(fileGot, "adding date from EXIF data");

									maxEdits--;
								}
								else
								{
									Log(logWriter, "ERROR: malformed EXIF date");
								}
							}
							else
							{
								Log(logWriter, "ERROR: no EXIF date");
							}
						}
						else
						{
							Log(logWriter, "WARNING: already has a Date");
						}
					}
					else
					{
						Log(logWriter, "ERROR: unclosed Information template");
					}
				}
				else
				{
					Log(logWriter, "ERROR: no Information template");
				}
			}
		}

		private static void Log(StreamWriter logWriter, string str)
		{
			Console.WriteLine(str);
			logWriter.WriteLine(str);
		}
	}
}
