using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			return credentials;
		}
	}
}
