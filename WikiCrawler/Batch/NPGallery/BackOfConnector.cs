using MediaWiki;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NPGallery
{
	internal class BackOfConnector
	{
		public static void Do()
		{
			Api api = new Api(new Uri("https://commons.wikimedia.org/"));
			api.AutoLogIn();

			string[] allLines = File.ReadAllLines("E:\\WikiData\\npgallery\\temp.txt");
			foreach (string line in allLines)
			{
				if (line.StartsWith("File:Back of \""))
				{
					string backFile = line;
					string frontName = ExtractFrontName(backFile);
					string frontFile = FindFrontFile(frontName, allLines);

					if (string.IsNullOrEmpty(frontFile))
					{
						Console.WriteLine("No front for '" + backFile + "'");
					}
					else
					{
						Console.WriteLine("Front is '" + frontFile + "'");

						Article[] arts = api.GetPages(new string[] { backFile, frontFile });
						{
							string backText = arts[0].revisions[0].text;
							string template = "{{Other|" + frontFile + "|Front side|suppress=yes}}";
							if (backText.Contains("|other_versions=\n"))
							{
								backText = backText.Replace("|other_versions=\n", "|other_versions=" + template + "\n");
								arts[0].revisions[0].text = backText;
							}
							else
							{
								Console.WriteLine("Failed to find other_versions: " + backFile);
							}
						}
						{
							string frontText = arts[1].revisions[0].text;
							string template = "{{Other|" + backFile + "|Back side|suppress=yes}}";
							if (frontText.Contains("|other_versions=\n"))
							{
								frontText = frontText.Replace("|other_versions=\n", "|other_versions=" + template + "\n");
								arts[1].revisions[0].text = frontText;
							}
							else
							{
								Console.WriteLine("Failed to find other_versions: " + frontFile);
							}
						}

						if (!api.SetPage(arts[0], "linking front side file"))
						{
							Console.WriteLine("Failed to update back side page.");
						}
						if (!api.SetPage(arts[1], "linking back side file"))
						{
							Console.WriteLine("Failed to update front side page.");
						}
					}
				}
			}
		}

		private static Regex m_backFile = new Regex("File:Back of \"([^\"]+)\" \\([A-Za-z0-9\\-]+\\).[A-Za-z]+");

		private static string ExtractFrontName(string backFile)
		{
			Match match = m_backFile.Match(backFile);
			if (match.Success)
			{
				return match.Groups[1].Value;
			}
			else
			{
				return string.Empty;
			}
		}

		private static string FindFrontFile(string name, string[] allLines)
		{
			string needle = "File:" + name + " (";
			foreach (string line in allLines)
			{
				if (line.StartsWith(needle))
				{
					return line;
				}
			}
			return string.Empty;
		}
	}
}
