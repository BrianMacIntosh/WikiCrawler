using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace WikiCrawler
{
    class UploadFromWeb
    {
        private static Uri page = new Uri("http://www.gutenberg.org/files/16634/16634-h/16634-h.htm");

        public static void Download()
        {
            //step 1: harvest image URLs
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(page);
            request.UserAgent = "BMacZeroBot (wikimedia)";

            //Read response
			Stream responseStream = EasyWeb.GetResponseStream(request);
            StreamReader read = new StreamReader(responseStream);
            string root = read.ReadToEnd();
            read.Close();

            List<string> images = new List<string>();
			List<string> captions = new List<string>();

            Marker imgStart = new Marker("<img ");
            Marker srcStart = new Marker("src=");
            Marker srcEnd = new Marker(" ");
			Marker captionStart = new Marker("<span class=\"caption\">");
			Marker captionEnd = new Marker("</span>");
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
                    if (imgStart.MatchAgainst(root[c]))
                    {
                        mode = 1;
						captionStart.Reset();
                    }
					else if (captionStart.MatchAgainst(root[c]))
					{
						startPt = c + 1;
						mode = -1;
						imgStart.Reset();
					}
                }
                else if (mode == 1)
                {
                    if (srcStart.MatchAgainst(root[c]))
                    {
                        startPt = c + 1;
                        mode = 2;
                    }
                }
                else if (mode == 2)
                {
                    if (srcEnd.MatchAgainst(root[c]))
                    {
                        finalize = true;
                        endPt = c - srcEnd.Length;
                        mode = 1;
                    }
                }
				else if (mode == -1)
				{
					if (captionEnd.MatchAgainst(root[c]))
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

			Console.WriteLine("Logging in...");
			Wikimedia.WikiApi Api = new Wikimedia.WikiApi(new Uri("https://commons.wikimedia.org/"));
			Api.LogIn();

            //Upload
            for (int c = 0; c < images.Length; c++)
            {
                string title = string.Format(ftitle, c.ToString("00"));

                Wikimedia.Article art = new Wikimedia.Article();
                art.title = title;
                art.revisions = new Wikimedia.Revision[1];
                art.revisions[0] = new Wikimedia.Revision();
                art.revisions[0].text = content;
                Api.UploadFromLocal(art, "C:/temp", "", false);
            }
        }
    }
}
