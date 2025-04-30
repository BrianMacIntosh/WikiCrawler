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

		protected virtual bool UseCheckpoint => true;

		/// <summary>
		/// The replacement operation to run.
		/// </summary>
		private BaseReplacement[] m_replacements;

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

		public ReplaceIn(params BaseReplacement[] replacements)
			: base()
		{
			m_replacements = replacements;

			Directory.CreateDirectory(ProjectDataDirectory);
		}

		public IEnumerable<Article> GetPagesToAffect(string startSortkey)
		{
#if false
			yield return GlobalAPIs.Commons.GetPage("File:Delivering supplies in a winter landscape 2000 CSK 08692 0103 000(124850).jpg", prop: "info|revisions");
			yield break;
#else
			if (UseCachedFiles)
			{
				return GetPagesToAffectCached();
			}
			else
			{
				return GlobalAPIs.Commons.FetchArticles(GetPagesToAffectUncached(startSortkey));
			}
#endif
		}

		public IEnumerable<Article> GetPagesToAffectCached()
		{
			char[] filesplitter = new char[] { '\n' };
			if (Directory.Exists(FileCacheDirectory))
			{
				foreach (string path in Directory.GetFiles(FileCacheDirectory))
				{
					string text = File.ReadAllText(path, Encoding.UTF8);
					string[] split = text.Split(filesplitter, 2);
					Article article = new Article(split[0]);
					article.revisions = new Revision[] { new Revision() { text = split[1] } };
					yield return article;
				}
			}
			else
			{
				yield break;
			}
		}

		public abstract IEnumerable<Article> GetPagesToAffectUncached(string startSortkey);

		public override void Execute()
		{
			string startSortkey = "";
			string progressFile = Path.Combine(ProjectDataDirectory, "checkpoint.txt");
			if (UseCheckpoint)
			{
				if (File.Exists(progressFile))
				{
					startSortkey = File.ReadAllText(progressFile);
				}
			}

			int saveOutInterval = 1;
			int saveOutCounter = 0;

			int readCount = 0;
			int editCount = 0;

			CreateHeartbeats();
			StartHeartbeat();

			foreach (Article file in GetPagesToAffect(startSortkey))
			{
				if (Article.IsNullOrMissing(file))
				{
					//TODO: delete from db
					continue;
				}

				if (editCount >= s_MaxEdits || readCount >= s_MaxReads)
				{
					break;
				}

				readCount++;

				// save out stats
				if (saveOutCounter >= saveOutInterval)
				{
					foreach (BaseReplacement replacement in m_replacements)
					{
						replacement.SaveOut();
					}

					saveOutCounter -= saveOutInterval;
				}
				saveOutCounter++;

				Console.WriteLine();
				ConsoleUtility.WriteLine(ConsoleColor.DarkGray, "Read {0}/{1}, Edit {2}/{3}", readCount, FormatInt(s_MaxReads), editCount, FormatInt(s_MaxEdits));

				foreach (BaseReplacement replacement in m_replacements)
				{
					ConsoleUtility.WriteLine(ConsoleColor.White, "{0} on '{1}'", replacement.GetType().Name, file.title);
					if (replacement.DoReplacement(file) && replacement.Heartbeat != null)
					{
						lock (replacement.Heartbeat)
						{
							replacement.Heartbeat.nEdits++;
						}
					}
				}

				if (file.Dirty && !UseCachedFiles)
				{
					//HACK: TEMP: log date replacements
					//File.AppendAllText(LocalizeDateReplacement.ReplacementsLogFile, file.title + "\n");

					GlobalAPIs.Commons.EditPage(file, file.GetEditSummary());
					editCount++;
				}

				// record sortkey progress
				if (!UseCachedFiles && UseCheckpoint)
				{
					File.WriteAllText(progressFile, WikiUtils.GetSortkey(file));
				}
			}

			SendHeartbeat(true);

			foreach (BaseReplacement replacement in m_replacements)
			{
				replacement.SaveOut();
			}
		}

		private string FormatInt(int value)
		{
			return value == int.MaxValue ? "INF" : value.ToString();
		}

		/// <summary>
		/// Creates a heartbeat task for each replacement operation that needs one.
		/// </summary>
		private void CreateHeartbeats()
		{
			foreach (BaseReplacement replacement in m_replacements)
			{
				if (replacement.UseHeartbeat)
				{
					replacement.Heartbeat = AddHeartbeatTask(replacement.GetType().Name);
				}
			}
		}
	}
}
