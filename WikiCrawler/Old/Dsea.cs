using System;
using System.IO;
using System.Net;

namespace Tasks
{
	public class DseaDownload : BaseTask
	{
		private const string url = "http://dsal.uchicago.edu/images/keagle/keagle_search.html?depth=details&id={0}";

		public override void Execute()
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
						string startMarker = "<td width=\"40%\">";
						int c = contents.IndexOf(startMarker);

						//NOTE: refactored and untested
						startMarker = "<td>";
						string endMarker = "</td>";
						int mode = 1;
						int startPoint = c + 1;
						for (; c < contents.Length; c++)
						{
							if (mode == 0)
							{
								if (contents.MatchAt(startMarker, c))
								{
									startPoint = c + 1;
									mode = 1;
								}
							}
							else if (mode == 1)
							{
								if (contents.MatchAt(endMarker, c))
								{
									writer.WriteLine(contents.Substring(startPoint, c - startPoint - endMarker.Length + 1));
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
