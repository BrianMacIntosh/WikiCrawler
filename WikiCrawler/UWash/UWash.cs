using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Globalization;
using WikiCrawler;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using UWash;

namespace WikiCrawler
{
	static class UWashController
	{
		private static string[] captionSplitters = new string[] { "--", "|" };

		//configuration
		private static UWashConfig config;
		private static int newCount = 0;
		private static int failedCount = 0;
		private static int succeededCount = 0;

		private static UWashProjectConfig projectConfig;
		private static string projectKey;
		private static string projectDir;

		private static bool Initialize()
		{
			string homeConfigFile = Path.Combine(Configuration.DataDirectory, "config.json");
			config = Newtonsoft.Json.JsonConvert.DeserializeObject<UWashConfig>(
				File.ReadAllText(homeConfigFile, Encoding.UTF8));

			Console.Write("Project Key>");
			projectKey = Console.ReadLine();
			projectDir = Path.Combine(Configuration.DataDirectory, projectKey);

			if (!Directory.Exists(projectDir))
			{
				Console.WriteLine("Project not found.");
				return false;
			}

			string projectConfigFile = Path.Combine(projectDir, "config.json");
			projectConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<UWashProjectConfig>(
				File.ReadAllText(projectConfigFile, Encoding.UTF8));

			return true;
		}

		public static void RebuildFailures()
		{
			if (!Initialize())
			{
				return;
			}

			Console.WriteLine("Finding holes...");
			List<int> missing = new List<int>();
			for (int i = projectConfig.minIndex; i <= projectConfig.maxIndex; i++)
			{
				missing.Add(i);
			}

			string suffixStart = " (" + projectConfig.filenameSuffix + " ";
			foreach (Wikimedia.Article article in Api.GetCategoryFiles(projectConfig.masterCategory))
			{
				Console.WriteLine(article.title);
				int tagIndex = article.title.IndexOf(suffixStart);
				if (tagIndex < 0)
				{
					continue;
				}
				int numStart = tagIndex + suffixStart.Length;
				int numEnd = article.title.IndexOf(')', numStart);
				int articleId = int.Parse(article.title.Substring(numStart, numEnd - numStart));
				missing.Remove(articleId);
			}

			string failedFile = Path.Combine(projectDir, "failed.json");
			using (StreamWriter writer = new StreamWriter(new FileStream(failedFile, FileMode.Create)))
			{
				writer.WriteLine("[");
				bool first = true;
				foreach (int i in missing)
				{
					if (!first) writer.WriteLine(",");
					writer.Write("\t{ \"Index\": " + i + ", \"Reason\": \"missing from commons\" }");
					first = false;
				}
				writer.WriteLine("]");
			}
		}

		/// <summary>
		/// Removes cached metadata for files that are already uploaded.
		/// </summary>
		public static void CacheCleanup()
		{
			if (!Initialize())
			{
				return;
			}

			int count = 0;
			foreach (string s in Directory.GetFiles(dataCacheDirectory))
			{
				int fileId = int.Parse(Path.GetFileNameWithoutExtension(s));
				if (!failures.Any(failure => failure.Index == fileId))
				{
					File.Delete(s);
					count++;
				}
			}
			Console.WriteLine("Deleted cached data: " + count);

			count = 0;
			foreach (string s in Directory.GetFiles(previewDirectory))
			{
				int fileId = int.Parse(Path.GetFileNameWithoutExtension(s));
				if (!failures.Any(failure => failure.Index == fileId))
				{
					File.Delete(s);
					count++;
				}
			}
			Console.WriteLine("Deleted preview data: " + count);
		}



		public static void Harvest()
		{
			try
			{
				//Try to reprocess old things
				Console.WriteLine();
				Console.WriteLine("Begin reprocessing of previously failed uploads.");
				Console.WriteLine();

				for (int c = 0; c < failures.Count && failedCount < config.maxFailed && succeededCount < config.maxSuccesses; c++)
				{
					Console.WriteLine();
					metadata.Clear();
					parseSuccessful = false;

					UWashFailure fail = failures[c];
					try
					{
						Process(fail.Index);

						//If we made it here, we succeeded
						failures.RemoveAt(c);
						c--;
					}
					catch (UWashException e)
					{
						Console.WriteLine("REFAILED:" + e.Message);
						failures[c] = new UWashFailure(fail.Index, e.Message);
					}

					failedCount++;

					saveOutCounter++;
					if (saveOutCounter >= config.saveOutInterval)
					{
						SaveOut();
						saveOutCounter = 0;
					}

					if (File.Exists(stopFile))
					{
						File.Delete(stopFile);
						return;
					}
				}

				//Process new things
				Console.WriteLine();
				Console.WriteLine("Begin processing new files.");
				Console.WriteLine();
				for (; current <= projectConfig.maxIndex && newCount < config.maxNew && succeededCount < config.maxSuccesses;)
				{
					Console.WriteLine();
					metadata.Clear();
					parseSuccessful = false;

					try
					{
						Process(current);
					}
					catch (UWashException e)
					{
						//There was an error
						string failReason = "";
						if (!parseSuccessful) failReason += "PARSE FAIL|";
						failReason += e.Message;
						failures.Add(new UWashFailure(current, failReason));
						Console.WriteLine("ERROR:" + e.Message);
					}

					current++;

					newCount++;

					saveOutCounter++;
					if (saveOutCounter >= config.saveOutInterval)
					{
						SaveOut();
						saveOutCounter = 0;
					}

					if (File.Exists(stopFile))
					{
						File.Delete(stopFile);
						Console.WriteLine("Got stop message.");
						return;
					}
				}
			}
			finally
			{
				SaveOut();
			}
		}

		private static Dictionary<string, string> PreprocessMetadata(Dictionary<string, string> data)
		{
			string lctgm, lcsh;

			if (!data.TryGetValue("LCTGM", out lctgm))
			{
				lctgm = "";
			}
			if (!data.TryGetValue("LCSH", out lcsh))
			{
				lcsh = "";
			}

			string temp;
			if (data.TryGetValue("Subjects (LCTGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects (LCTGM)");
			}
			if (data.TryGetValue("Subjects(LCTGM)", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("Subjects(LCTGM)");
			}
			if (data.TryGetValue("lctgm", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("lctgm");
			}
			if (data.TryGetValue("subjec", out temp))
			{
				lctgm = StringUtility.Join("|", lctgm, temp);
				data.Remove("subjec");
			}
			if (data.TryGetValue("Subjects (LCSH)", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("Subjects (LCSH)");
			}
			if (data.TryGetValue("Subject (LCSH)", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("Subject (LCSH)");
			}
			if (data.TryGetValue("subjea", out temp))
			{
				lcsh = StringUtility.Join("|", lcsh, temp);
				data.Remove("subjea");
			}

			if (!string.IsNullOrEmpty(lcsh))
			{
				UWashController.metadata["LCSH"] = lcsh.Replace(lineBreak, "|");
			}
			if (!string.IsNullOrEmpty(lctgm))
			{
				UWashController.metadata["LCTGM"] = lctgm.Replace(lineBreak, "|");
			}

			return UWashController.metadata;
		}
	}
}

class UWashException : Exception
{
	public UWashException(string message)
		: base(message)
	{

	}
}
