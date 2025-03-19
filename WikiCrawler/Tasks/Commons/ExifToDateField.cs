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
				CommonsFileWorksheet worksheet = new CommonsFileWorksheet(fileGot);

				string date = worksheet.Date;
				if (string.IsNullOrWhiteSpace(date))
				{
					if (fileGot.imageinfo[0].metadata.TryGetValue("DateTimeOriginal", out string exifDate))
					{
						string[] dateTime = exifDate.Split(' ');
						if (dateTime.Length == 2 && dateTime[0].Length == 10)
						{
							string dateTag = "{{According to Exif data|" + dateTime[0].Replace(':', '-') + "}}";
							worksheet.Date = dateTag;
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
					Log(logWriter, "WARNING: already has a Date or nowhere to put Date");
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
