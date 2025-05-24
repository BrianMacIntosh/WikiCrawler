using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace WikiCrawler
{
	class UploadFromWeb
    {
        private static Uri page = new Uri("http://www.gutenberg.org/files/16634/16634-h/16634-h.htm");

        public static void Download()
        {
            //step 1: harvest image URLs
            string root;
            using (StreamReader read = new StreamReader(WebInterface.HttpGet(page)))
            {
                root = read.ReadToEnd();
            }

            List<string> images = new List<string>();
			List<string> captions = new List<string>();

            //NOTE: refactored and untested
            string imgStart = "<img ";
            string srcStart = "src=";
            string srcEnd = " ";
			string captionStart = "<span class=\"caption\">";
			string captionEnd = "</span>";
            int mode = 0;
            int startPt = 0;
            for (int c = 0; c < root.Length; c++)
            {
                bool finalize = false;
				bool finalizeCaption = false;
                int endPt = 0;
                if (mode > 0 && (root[c] == '"' || root[c] == '>'))
                {
                    if (mode == 2)
                    {
                        finalize = true;
                        endPt = c - 1;
                    }
                    mode = 0;
                }
                else if (mode == 0)
                {
                    if (root.MatchAt(imgStart, c))
                    {
                        mode = 1;
                    }
					else if (root.MatchAt(captionStart, c))
					{
						startPt = c + captionStart.Length;
						mode = -1;
					}
                }
                else if (mode == 1)
                {
                    if (root.MatchAt(srcStart, c))
                    {
                        startPt = c + srcStart.Length;
                        mode = 2;
                    }
                }
                else if (mode == 2)
                {
                    if (root.MatchAt(srcEnd, c))
                    {
                        finalize = true;
                        endPt = c - srcEnd.Length;
                        mode = 1;
                    }
                }
				else if (mode == -1)
				{
					if (root.MatchAt(captionEnd, c))
					{
						finalizeCaption = true;
						endPt = c - captionEnd.Length;
						mode = 0;
					}
				}
                if (finalize)
                {
                    images.Add(root.Substring(startPt, endPt - startPt + 1).Trim(' ', '\r', '\n', '"', '\''));
                }
				if (finalizeCaption)
				{
					captions.Add(WebUtility.HtmlDecode(root.Substring(startPt, endPt - startPt + 1))
						.Replace('\n', ' ').Replace("\r", "").Trim(' ', '"'));
				}
            }

			StreamWriter writer = new StreamWriter("captions.txt");
			foreach (string s in captions)
				writer.WriteLine(s);
			writer.Close();

			//Download
			if (!Directory.Exists("images"))
				Directory.CreateDirectory("images");
			foreach (string s in Directory.GetFiles("images"))
				File.Delete(s);

            WebClient client = new WebClient();
            for (int c = 0; c < images.Count; c++)
            {
                Uri imageUri = new Uri(page, images[c]);
                Console.WriteLine(imageUri);

				System.Threading.Thread.Sleep(10000);

				client.DownloadFile(imageUri, Path.Combine("images", "temp" + c.ToString("0000") + Path.GetExtension(images[c])));
            }
        }

        public static void Upload()
        {
            if (!Directory.Exists("images"))
            {
                throw new InvalidOperationException();
            }

            string ftitle = "Blitmore Oswald illustration {0}";
            string content = @"=={{int:filedesc}}==
{{Information
|description={{en|1=An illustration in the book ''Blitmore Oswald: Diary of a Hapless Recruit'' by J. Thorne Smith Jr., illustrated by Richard Dorgan.
Caption: " + @"}}
|date=1918
|source=" + page + @"
|author={{Creator:Richard Dorgan}}
|permission=
|other_versions=
|other_fields=
}}

=={{int:license-header}}==
{{PD-Art|PD-1923}}

[[Category:Illustrations from Blitmore Oswald]][[Category:Files from Project Gutenberg]]";

            string[] images = Directory.GetFiles("images");

            Api Api = GlobalAPIs.Commons;

            //Upload
            for (int c = 0; c < images.Length; c++)
            {
                PageTitle title = new PageTitle(PageTitle.NS_File, string.Format(ftitle, c.ToString("00")));
                Article art = new Article(title, content);
                Api.UploadFromLocal(art, "C:/temp", "", false);
            }
        }
    }
}
