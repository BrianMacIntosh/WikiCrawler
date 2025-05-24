using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Tasks
{
	public class UploadFromZorger : BaseTask
	{
        public UploadFromZorger()
        {
            Parameters["RootUrl"] = "http://www.example.com/";
        }

        public override void Execute()
		{
            Uri rooturi = new Uri(Parameters["RootUrl"]);

            //step 1: harvest image links
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(rooturi);
            request.UserAgent = "BMacZeroBot (wikimedia)";

            //Read response
            string root;
            using (StreamReader read = new StreamReader(WebInterface.HttpGet(rooturi)))
            {
                root = read.ReadToEnd();
            }

            //NOTE: refactored and utested
            //Get page links
            List<string> pages = new List<string>();
            string tableStart = "<table width=100% border=1 align = 'center' valign = 'center' >";
            string tableEnd = "</table>";
            string linkStart = "<a href='";
            string linkEnd = "'><img";
            int mode = 0;
            int startPt = 0;
            for (int c = 0; c < root.Length; c++)
            {
                if (mode == 0)
                {
                    if (root.MatchAt(tableStart, c))
                        mode = 1;
                }
                if (mode > 0)
                {
                    if (root.MatchAt(tableEnd, c))
                        break;
                }
                if (mode == 1)
                {
                    if (root.MatchAt(linkStart, c))
                    {
                        startPt = c + 1;
                        mode = 2;
                    }
                }
                if (mode == 2)
                {
                    if (root.MatchAt(linkEnd, c))
                    {
                        string link = root.Substring(startPt, c - linkEnd.Length - startPt + 1);

                        if (link.StartsWith("/")) link = rooturi.Scheme + "://" + rooturi.Host + link;

                        pages.Add(link);
                        Console.WriteLine(link);
                        mode = 1;
                    }
                }
            }

			Api Api = GlobalAPIs.Commons;

            //fetch images urls from those pages
            int count = 0;
            foreach (string page in pages)
            {
                //Read response
                string content;
                using (StreamReader read = new StreamReader(WebInterface.HttpGet(new Uri(page))))
                {
                    content = read.ReadToEnd();
                }

                string imageStart = "<img src=";
                string imageEnd = @" 
	alt=";
                mode = 0;
                startPt = 0;
                for (int c = 0; c < content.Length; c++)
                {
                    if (mode == 0)
                    {
                        if (content.MatchAt(imageStart, c))
                        {
                            mode = 1;
                            startPt = c + 1;
                        }
                    }
                    else if (mode == 1)
                    {
                        if (content.MatchAt(imageEnd, c))
                        {
                            string img = content.Substring(startPt, c - imageEnd.Length - startPt + 1);
                            string[] components = page.Split('/');
                            components[components.Length-1] = img;
                            img = string.Join("/", components);

							Article article = new Article();
                            article.title = new PageTitle(PageTitle.NS_File, "Last Enemy illustration " + count.ToString("00") + Path.GetExtension(img));
                            article.revisions = new Revision[1];
                            article.revisions[0] = new Revision();
                            article.revisions[0].text = @"=={{int:filedesc}}==
{{Information
|description={{en|1=An illustration in the book ''Last Enemy'' by H. Beam Piper, illustrated by Miller.}}
|date=1950
|source=http://public-domain.zorger.com/last-enemy/index.php, ultimately http://www.gutenberg.org/files/18800/18800-h/18800-h.htm
|author=Miller
|permission=
|other_versions=
|other_fields=
}}

=={{int:license-header}}==
{{PD-Art|PD-US-not renewed}}

[[Category:Illustrations from Last Enemy]]";

                            //Download the image
                            WebClient client = new WebClient();
                            client.DownloadFile(img, "C:/temp");

                            Api.UploadFromLocal(article, "C:/temp", "", false);

                            count++;
                        }
                    }
                }
            }
        }
	}
}
