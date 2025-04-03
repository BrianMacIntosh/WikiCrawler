using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WikiCrawler;

namespace Tasks
{
	/// <summary>
	/// Reports pages that have malformed wikitext.
	/// </summary>
	public class ReportMalformedPages : BaseReplacement
	{
		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, "reportmalformed"); }
		}

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public static string ReportFile
		{
			get { return Path.Combine(ProjectDataDirectory, "report.txt"); }
		}

		public ReportMalformedPages()
		{
			Directory.CreateDirectory(ProjectDataDirectory);
		}

		private void Log(string message)
		{
			File.AppendAllText(ReportFile, message, Encoding.UTF8);

			ConsoleUtility.WriteLine(ConsoleColor.Red, "  " + message);
		}

		public override bool DoReplacement(Article article)
		{
			string text = article.revisions[0].text;

			Stack<WikitextNestingType> stack = new Stack<WikitextNestingType>();

			for (int i = 0; i < text.Length; )
			{
				if (StringUtility.MatchAt(text, "{{", i))
				{
					if (stack.Count > 0 && stack.Peek() == WikitextNestingType.Link)
					{
						Log(string.Format("Template in link in '{0}'.\n", article.title));
					}

					stack.Push(WikitextNestingType.Template);
					i += 2;
				}
				else if (StringUtility.MatchAt(text, "[[", i))
				{
					stack.Push(WikitextNestingType.Link);
					i += 2;
				}
				else if (StringUtility.MatchAt(text, "}}", i))
				{
					if (stack.Count <= 0)
					{
						Log(string.Format("Unclosed template in '{0}'.\n", article.title));
					}
					else
					{
						WikitextNestingType lastType = stack.Pop();
						if (lastType != WikitextNestingType.Template)
						{
							Log(string.Format("Mismatched link/template in '{0}'.\n", article.title));
						}
					}

					i += 2;
				}
				else if (StringUtility.MatchAt(text, "]]", i))
				{
					if (stack.Count <= 0)
					{
						Log(string.Format("Unclosed link in '{0}'.\n", article.title));
					}
					else
					{
						WikitextNestingType lastType = stack.Pop();
						if (lastType != WikitextNestingType.Link)
						{
							Log(string.Format("Mismatched template/link in '{0}'.\n", article.title));
						}
					}

					i += 2;
				}
				else
				{
					i++;
				}
			}

			return false;
		}
	}
}
