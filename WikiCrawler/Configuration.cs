using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WikiCrawler
{
	public static class Configuration
	{
		/// <summary>
		/// Base directory for config and cached data.
		/// </summary>
		public const string DataDirectory = "E:/WikiData";

		public static Credentials LoadCredentials()
		{
			string authFile = Path.Combine(DataDirectory, "auth.json");
			Credentials credentials = new Credentials();
			if (File.Exists(authFile))
			{
				credentials = Newtonsoft.Json.JsonConvert.DeserializeObject<Credentials>(
					File.ReadAllText(authFile, Encoding.UTF8));
			}
			else
			{
				ConsoleUtility.WriteLine(ConsoleColor.Red, "Failed to load credentials from '{0}'.", authFile);
			}
			return credentials;
		}
	}
}
