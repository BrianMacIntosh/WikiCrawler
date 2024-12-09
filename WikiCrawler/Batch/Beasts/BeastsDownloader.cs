using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

internal class BeastsDownloader
{
	private Dictionary<int, string> img_src = new Dictionary<int, string>();

	public void Execute()
	{
		string[] lines = File.ReadAllLines("E:/beasts/pictures.csv", Encoding.UTF8);
		foreach (string line in lines)
		{
			if (!string.IsNullOrWhiteSpace(line))
			{
				string[] split = line.Split(',');
				int itemKey = int.Parse(split[0].Trim("\""));
				string url = split[1].Trim("\"");
				img_src.Add(itemKey, url);
			}
		}

		List<int> broken = File.ReadAllLines("E:/beasts/broken.txt").Select((str) => int.Parse(str) + 1).ToList();

		List<string> query = new List<string>();
		foreach (int key in img_src.Keys)
		{
			if (!broken.Contains(key))
				query.Add("UPDATE `pictures` SET cached_img=\"beast" + key.ToString("000000") + ".webp\" WHERE id=" + key + ";");
		}
		File.WriteAllLines("E:/beasts/query.txt", query);
		return;

		WebClient WebClient = new WebClient();

		foreach (int key in img_src.Keys)
		{
			Console.WriteLine(key);
			string url = img_src[key];
			string ext = Path.GetExtension(url);
			Uri uri = new Uri(url);

			string fname = "E:/beasts/beast" + key.ToString("000000") + ext;
			if (File.Exists(fname))
			{
				continue;
			}

			EasyWeb.WaitForDelay(uri);
			WebClient.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
			WebClient.Headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
			WebClient.Headers.Add("Accept-Language", "en-US,en;q=0.9");
			WebClient.Headers.Add("Dnt", "1");
			WebClient.Headers.Add("Priority", "i");
			WebClient.Headers.Add("Referer", "https://" + uri.Host);
			WebClient.Headers.Add("Sec-Ch-Ua", "\"Not/A)Brand\";v=\"8\", \"Chromium\";v=\"126\", \"Microsoft Edge\";v=\"126\"");
			WebClient.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
			WebClient.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
			WebClient.Headers.Add("Sec-Fetch-Dest", "image");
			WebClient.Headers.Add("Sec-Fetch-Mode", "no-cors");
			WebClient.Headers.Add("Sec-Fetch-Site", "same-site");
			WebClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0");
			try
			{
				WebClient.DownloadFile(uri, fname);
			}
			catch (WebException e)
			{
				broken.Add(key);
			}
		}

		File.WriteAllLines("E:/beasts/broken.txt", broken.Select((i)=> i.ToString()).ToArray());
	}
}
