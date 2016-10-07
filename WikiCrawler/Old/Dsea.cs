using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;

namespace WikiCrawler
{
	class Dsea
	{
		private const string url = "http://dsal.uchicago.edu/images/keagle/keagle_search.html?depth=details&id={0}";

		public static void Harvest()
		{
			int current = 1;

			if (File.Exists("progress.txt"))
			{
				BinaryReader reader = new BinaryReader(new FileStream("progress.txt", FileMode.Open));
				current = reader.ReadInt32();
				reader.Close();
			}

			int maxgo = int.MaxValue;

			try
			{
				using (StreamWriter writer = new StreamWriter(new FileStream("keagle.txt", FileMode.Append)))
				{
					for (; current <= 92;)
					{
						Console.WriteLine(current);

						//Upload stream
						HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(string.Format(url, current.ToString("0000"))));
						request.UserAgent = "Brian MacIntosh (Wikimedia Commons) - bot";

						//Read response
						StreamReader read = new StreamReader(EasyWeb.GetResponseStream(request));
						string contents = read.ReadToEnd();

						writer.WriteLine(string.Format("http://dsal.uchicago.edu/images/keagle/images/large/{0}.jpg", current.ToString("0000")));

						//Parse it
						Marker start = new Marker("<td width=\"40%\">");
						int c = 0;
						for (; c < contents.Length; c++)
						{
							if (start.MatchAgainst(contents[c]))
								break;
						}

						start = new Marker("<td>");
						Marker end = new Marker("</td>");
						int mode = 1;
						int startPoint = c + 1;
						for (; c < contents.Length; c++)
						{
							if (mode == 0)
							{
								if (start.MatchAgainst(contents[c]))
								{
									startPoint = c + 1;
									mode = 1;
								}
							}
							else if (mode == 1)
							{
								if (end.MatchAgainst(contents[c]))
								{
									writer.WriteLine(contents.Substring(startPoint, c - startPoint - end.Length + 1));
									mode = 0;
								}
							}
						}

						writer.WriteLine();

						current++;

						maxgo--;
						if (maxgo <= 0) break;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
			finally
			{
				//write current
				BinaryWriter writer = new BinaryWriter(new FileStream("progress.txt", FileMode.Create));
				writer.Write(current);
				writer.Close();
			}
		}
	}
}
