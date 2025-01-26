using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WikiCrawler;

namespace Tasks
{

#if false
	/// <summary>
	/// Caches files for ReplaceInCategory to test on.
	/// </summary>
	public class ReplaceInCategoryFixupCache : BaseTask
	{
		public override void Execute()
		{
			foreach (Article file in ReplaceInCategory.GetFilesToAffectUncached(""))
			{
				Console.WriteLine(file.title);

				string filename = Path.Combine(ReplaceInCategory.FileCacheDirectory, string.Concat(file.title.Split(Path.GetInvalidFileNameChars())));
				if (filename.Length > 250)
				{
					filename = filename.Substring(0, 250);
				}
				filename = filename + ".txt";
				if (File.Exists(filename))
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("  File exists");
					Console.ResetColor();
				}
				File.WriteAllText(filename, file.title + "\n" + file.revisions[0].text, Encoding.UTF8);
			}
		}
	}
#endif

	/// <summary>
	/// Performs a replacement operation on a set of specified files.
	/// </summary>
	public abstract class ReplaceIn : BaseTask
	{
		private static readonly int s_MaxReads = int.MaxValue;
		private static readonly int s_MaxEdits = int.MaxValue;

		/// <summary>
		/// If true, will use previously cached files as a test. Will not make edits.
		/// </summary>
		private const bool UseCachedFiles = false;

		/// <summary>
		/// The replacement operation to run.
		/// </summary>
		private BaseReplacement m_replacement;

		/// <summary>
		/// Directory where task-specific data is stored.
		/// </summary>
		public string ProjectDataDirectory
		{
			get { return Path.Combine(Configuration.DataDirectory, GetType().Name); }
		}

		public string FileCacheDirectory
		{
			get { return Path.Combine(ProjectDataDirectory, "cache"); }
		}

		public ReplaceIn(BaseReplacement replacement)
		{
			HeartbeatEnabled = true;

			m_replacement = replacement;

			Directory.CreateDirectory(FileCacheDirectory);
		}

		public IEnumerable<Article> GetFilesToAffect(string startSortkey)
		{
#if false
			yield return GlobalAPIs.Commons.GetPage("File:Study of a Young Girl, Pont Aven SAAM-1977.113 1.jpg", prop: "info|revisions");
			yield break;
#else
			if (UseCachedFiles)
			{
				return GetFilesToAffectCached();
			}
			else
			{
				return GetFilesToAffectUncached(startSortkey);
			}
#endif
		}

		public IEnumerable<Article> GetFilesToAffectCached()
		{
			char[] filesplitter = new char[] { '\n' };
			foreach (string path in Directory.GetFiles(FileCacheDirectory))
			{
				string text = File.ReadAllText(path, Encoding.UTF8);
				string[] split = text.Split(filesplitter, 2);
				Article article = new Article(split[0]);
				article.revisions = new Revision[] { new Revision() { text = split[1] } };
				yield return article;
			}
		}

		public abstract IEnumerable<Article> GetFilesToAffectUncached(string startSortkey);

		public override void Execute()
		{
			string startSortkey = "";
			string progressFile = Path.Combine(ProjectDataDirectory, "checkpoint.txt");
			if (File.Exists(progressFile))
			{
				startSortkey = File.ReadAllText(progressFile);
			}

			int saveOutInterval = 1;
			int saveOutCounter = 0;

			int readCount = 0;
			int editCount = 0;

			StartHeartbeat();

			foreach (Article file in GetFilesToAffect(startSortkey))
			{
				if (editCount >= s_MaxEdits || readCount >= s_MaxReads)
				{
					break;
				}

				readCount++;

				// save out stats
				if (saveOutCounter >= saveOutInterval)
				{
					m_replacement.SaveOut();

					saveOutCounter -= saveOutInterval;
				}
				saveOutCounter++;

				// record sortkey progress
				if (!UseCachedFiles)
				{
					File.WriteAllText(progressFile, WikiUtils.GetSortkey(file));
				}

				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine();
				Console.WriteLine("{0} on '{1}'", m_replacement.GetType().Name, file.title);
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine("Read {0}/{1}, Edit {2}/{3}", readCount, FormatInt(s_MaxReads), editCount, FormatInt(s_MaxEdits));
				Console.ResetColor();

				m_replacement.DoReplacement(file);

				if (file.Dirty && !UseCachedFiles)
				{
					//HACK: TEMP: log date replacements
					//File.AppendAllText(LocalizeDateReplacement.ReplacementsLogFile, file.title + "\n");

					GlobalAPIs.Commons.EditPage(file, file.GetEditSummary());
					m_heartbeatData["nEdits"] = (int)m_heartbeatData["nEdits"] + 1;
					editCount++;
				}

				CreatorUtilityMeta.SaveOut();
			}

			SendHeartbeat(true);

			m_replacement.SaveOut();
		}

		private string FormatInt(int value)
		{
			return value == int.MaxValue ? "INF" : value.ToString();
		}
	}
}
