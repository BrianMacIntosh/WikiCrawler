using System;
using System.Collections.Generic;
using System.IO;

/*
 * Very lightweight and standard-inclusive robots.txt parser.
 * Stores nested Allow/Disallow directives in a tree structure.
 * 
 * Based on the robots standard at:
 * http://www.robotstxt.org/orig.html
 * 
 * Features:
 * - Multiple User-Agent tags
 * - Arbitrarily nested Allow and Disallow tags
 * - Crawl-delay tags
 * - Sitemap tags
 * - # comments
 * 
 * by Brian MacIntosh
 * 23 March 2013
 * 
 * Questions, comments, bug reports: brianamacintosh@gmail.com
 * 
 * Free for all commercial and noncommercial use.
 */

namespace LightweightRobots
{
    /// <summary>
    /// This class parses robots.txt files and offers the contained information.
    /// </summary>
    sealed class RobotsTxt
    {
        /// <summary>
        /// The unique name of your bot.
        /// The standard recommends removing version information for this check.
        /// </summary>
        public string UserAgent
        {
            get { return mUserAgent; }
            set { mUserAgent = value.ToLower(); }
        }
        private string mUserAgent = "*";

        private Dictionary<string, int> crawldelay = new Dictionary<string, int>();

        private Dictionary<string, HashTreeNode> allow = new Dictionary<string, HashTreeNode>();

        private string[] sitemaps = new string[0];

        /// <summary>
        /// Gets or sets a value indicated whether to respect Allow tags.
        /// </summary>
        public bool UseAllowTags = true;

        private Uri host;

        /// <summary>
        /// Reads robots.txt from a stream.
        /// Consumes and closes the stream.
        /// </summary>
        /// <param name="host">Any URL from the site being crawled.</param>
        public RobotsTxt(Uri host, Stream data)
        {
            this.host = host;
            Parse(data);
        }

        private void Parse(Stream data)
        {
            List<string> sitemapstemp = new List<string>();

            //Current user-agent being read
            string currentagent = "*";
            allow[currentagent] = new HashTreeNode(true, "/");
            crawldelay[currentagent] = 0;

            //Parse file
            char[] split = { ' ', (char)9 };
            StreamReader read = new StreamReader(data);
            while (!read.EndOfStream)
            {
                string[] line = read.ReadLine().Trim().Split(split, StringSplitOptions.RemoveEmptyEntries);

                if (line.Length < 2) continue;
                if (line[0][0] == '#') continue;

                if (string.Equals(line[0], "user-agent:", StringComparison.OrdinalIgnoreCase))
                {
                    currentagent = line[1].ToLower();
                    allow[currentagent] = new HashTreeNode(true, "/");
                }
                else if (string.Equals(line[0], "disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    allow[currentagent].AddPattern(false, line[1]);
                }
                else if (string.Equals(line[0], "allow:", StringComparison.OrdinalIgnoreCase))
                {
                    allow[currentagent].AddPattern(true, line[1]);
                }
                else if (string.Equals(line[0], "crawl-delay:", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        crawldelay[currentagent] = int.Parse(line[1]);
                    }
                    catch (FormatException)
                    {

                    }
                    catch (OverflowException)
                    {

                    }
                }
                else if (string.Equals(line[0], "sitemap:", StringComparison.OrdinalIgnoreCase))
                {
                    sitemapstemp.Add(line[1]);
                }
            }
            read.Close();

            sitemaps = sitemapstemp.ToArray();
        }

        /// <summary>
        /// Return true if the current User-agent is permitted to access the given file, otherwise false.
        /// throws: ArgumentException if host doesn't match host of the robot.txt file used
        /// </summary>
        public bool IsAllowed(Uri path)
        {
            if (!path.Host.Equals(host.Host))
            {
                throw new ArgumentException("The host of the specified URI is not the same as the robots.txt host.\n");
            }
            else
            {
                foreach (KeyValuePair<string, HashTreeNode> kv in allow)
                    if (kv.Key.Contains(UserAgent)) return kv.Value.Traverse(path.PathAndQuery);
                return allow["*"].Traverse(String.Join("", path.Segments));
            }
        }

        /// <summary>
        /// Gets a time, in seconds, that the crawler is requested to wait between page requests.
        /// (Crawl-delay tag)
        /// </summary>
        public int CrawlDelay()
        {
            if (crawldelay.ContainsKey(UserAgent))
                return crawldelay[UserAgent];
            else
                return crawldelay["*"];
        }

        /// <summary>
        /// Gets suggested robot-inclusion sitemaps from the file.
        /// (Sitemap tag)
        /// </summary>
        public string[] Sitemaps()
        {
            return sitemaps;
        }


        private class HashTreeNode
        {
            public bool allow;
            public string segments;
            public List<HashTreeNode> children = new List<HashTreeNode>();

            public HashTreeNode(bool allow, string segments)
            {
                this.allow = allow;
                this.segments = segments;
            }

            public void AddPattern(bool allow, string segments)
            {
                foreach (HashTreeNode node in children)
                {
                    if (segments.Equals(node.segments))
                    {
                        //Update node value
                        node.allow = allow;
                        return;
                    }
                    else if (segments.StartsWith(node.segments))
                    {
                        //Add as a new child of the target node
                        node.AddPattern(allow, segments);
                        return;
                    }
                    else if (node.segments.StartsWith(segments))
                    {
                        //Add as a new child of this node
                        HashTreeNode newnode = new HashTreeNode(allow, segments);

                        //Move any matching nodes under the new child
                        foreach (HashTreeNode n1 in children)
                            if (n1.segments.Contains(segments)) newnode.AddPattern(n1.allow, n1.segments);

                        children.Add(newnode);
                        return;
                    }
                }

                //No match, new child here
                children.Add(new HashTreeNode(allow, segments));
                return;
            }

            public bool Traverse(string match)
            {
                foreach (HashTreeNode node in children)
                    if (match.StartsWith(node.segments)) return node.Traverse(match);
                return allow;
            }
        }
    }
}
