using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WikiCrawler
{
	class MassDownloader
	{
		public static void Do()
		{
			/*List<string> lines = new List<string>();
			foreach (string path in Directory.GetFiles("E:/downloadedimages"))
			{
				int id = int.Parse(Path.GetFileNameWithoutExtension(path).Substring("beast".Length, 6));
				lines.Add("UPDATE pictures p SET cached_img = \"beast"
					+ id.ToString("000000")
					+ Path.GetExtension(path) + "\" WHERE id=" + id + ";");
			}
			File.WriteAllLines("E:/sql.sql", lines.ToArray());
			return;*/

			WebClient client = new WebClient();
			client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
			Regex photobucketUrl = new Regex(@"\/albums\/\w+\/(\w+)\/\w+\.\w+");

			if (!Directory.Exists(@"E:\downloadedimages"))
			{
				Directory.CreateDirectory(@"E:\downloadedimages");
			}
			List<string> errors = new List<string>();
			foreach (string line in File.ReadAllLines(@"E:\beastback.txt"))
			{
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}
				string[] split = line.Split(';');
				int id = int.Parse(split[0]);
				string url = split[1];
				Uri uri = new Uri(url);
				Console.WriteLine(uri);
				string targetFile = @"E:\downloadedimages\beast" + id.ToString("000000") + Path.GetExtension(uri.AbsolutePath);
				if (!File.Exists(targetFile))
				{
					try
					{
						// circumvent photobucket hotlink prevention
						if (url.Contains("photobucket"))
						{
							Match match = photobucketUrl.Match(uri.AbsolutePath);
							if (match.Success)
							{
								uri = new Uri(url.Replace("http://", "https://"));
								client.Headers["referer"] = "http://photobucket.com/gallery/user/" + match.Groups[1].Value + "/media/";
								client.Headers["upgrade-insecure-requests"] = "1";
								client.Headers["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.186 Safari/537.36";
							}
						}
						else
						{
							continue;
						}

						client.DownloadFile(url, targetFile);
					}
					catch (WebException e)
					{
						if (e.Response is HttpWebResponse)
						{
							errors.Add(id.ToString() + "," + ((HttpWebResponse)e.Response).StatusCode);
						}
						else
						{
							errors.Add(id.ToString() + ",Unknown");
						}
					}
				}
			}
			File.WriteAllLines(@"E:\errors.csv", errors.ToArray());
		}
	}
}
