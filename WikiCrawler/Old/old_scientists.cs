using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WikiCrawler
{
    class Scientist
    {
        public string Name;
        public string DOB;
    }

    class Program2
    {
        static void Main2(string[] args)
        {
            /*List<WikimediaArticle> articles = WikimediaApi.GetCategoryPagesRecursive("Category:People in information technology");

            foreach (WikimediaArticle a in articles)
            {
                WikimediaArticle art = WikimediaApi.GetPage(a);
                if (art.revisions != null && art.revisions.Any())
                {
                    string text = art.revisions.First().text;

                }
            }*/

            /*List<string> arts = new List<string>();
            WikimediaArticle art = WikimediaApi.GetPage("Lists of mathematicians");
            foreach (string a2 in GetBulletLinks(art.revisions[0].text))
            {
                arts.AddRange(GetBulletLinks(WikimediaApi.GetPage(a2).revisions[0].text));
            }
            StreamWriter write2 = new StreamWriter("C:/pages.txt");
            foreach (string st in arts)
                write2.WriteLine(st);
            write2.Close();
            return;*/

			Wikimedia.WikiApi Api = new Wikimedia.WikiApi(new Uri("http://en.wikipedia.org/"));

            StreamReader re = new StreamReader(new FileStream("C:/pages.txt", FileMode.Open));

            List<Scientist> scientists = new List<Scientist>();
            Marker titlemark = new Marker("'''");
            string sback = "START";
            bool started = false;
            string text;
            Wikimedia.Article art;
            while (!re.EndOfStream)
            {
                string s = re.ReadLine();
                if (!started)
                {
                    started = s.Equals("Raoul Bott");
                    continue;
                }

                sback = s;
                art = Api.GetPage(s);
                Console.WriteLine(s);

                if (art.revisions == null || art.revisions.Length == 0)
                    continue;

                text = art.revisions[0].text;

                Scientist sci = new Scientist();
                sci.Name = s;
                int paren = s.IndexOf('(');
                if (paren >= 0)
                    sci.Name = s.Remove(paren).Trim();
                scientists.Add(sci);

                //Find name in article
                int openpoint = -1;
                int mode = 0;
                int digits = 0;
                for (int c = 0; c < text.Length; c++)
                {
                    if (mode == 0)
                    {
                        //Find name
                        if (openpoint >= 0)
                        {
                            if (titlemark.MatchAgainst(text[c]))
                            {
                                string newname = text.Substring(openpoint, c - openpoint - 2);
                                if (newname.Length > sci.Name.Length)
                                {
                                    sci.Name = newname;
                                    Console.WriteLine(newname);
                                }
                                mode = c;
                            }
                        }
                        else if (titlemark.MatchAgainst(text[c]))
                        {
                            openpoint = c + 1;
                        }
                    }
                    else
                    {
                        //Find DOB
                        if (text[c] >= '0' && text[c] <= '9')
                        {
                            digits++;
                            if (digits == 4)
                            {
								if (text[c + 1] >= '0' && text[c + 1] <= '9')
								{
									
								}
								else
								{
									sci.DOB = text.Substring(c - 3, 4);
									Console.WriteLine(sci.DOB);
									break;
								}
                            }
                        }
                        else
                        {
                            digits = 0;
                        }
                    }
                }

                StreamWriter write = new StreamWriter(new FileStream("C:/math.txt", FileMode.Create));
                foreach (Scientist asdf in scientists)
                {
                    write.WriteLine(asdf.Name + "|" + asdf.DOB);
                }
                write.WriteLine("+" + sback);
                write.Close();
            }
        }

        private static List<string> GetBulletLinks(string text)
        {
            List<string> names = new List<string>();
            Marker marker1 = new Marker("* [[");
            Marker marker2 = new Marker("*[[");
            int namestart = -1;
            for (int c = 0; c < text.Length; c++)
            {
                if (namestart >= 0)
                {
                    if (text[c] == '|' || text[c] == ']')
                    {
                        names.Add(text.Substring(namestart, c - namestart).Trim());
                        namestart = -1;
                    }
                }
                else if (marker1.MatchAgainst(text[c]) || marker2.MatchAgainst(text[c]))
                {
                    marker1.Reset();
                    marker2.Reset();
                    namestart = c + 1;
                }
            }
            return names;
        }
    }
}
