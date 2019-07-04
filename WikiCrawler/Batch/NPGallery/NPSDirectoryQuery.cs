using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace NPGallery
{
	public static class NPSDirectoryQuery
	{
		private const string Endpoint = @"https://www.nps.gov/directory/employees.cfm?sn={1}&givenname={0}";

		private struct PersonName : IEquatable<PersonName>
		{
			public string First;
			public string Last;

			// for serialization
			public bool IsNPS;

			public PersonName(string first, string last, bool isNps)
			{
				First = first;
				Last = last;
				IsNPS = isNps;
			}

			public override bool Equals(object obj)
			{
				return obj is PersonName && Equals((PersonName)obj);
			}

			public bool Equals(PersonName other)
			{
				return First == other.First && Last == other.Last;
			}

			public override int GetHashCode()
			{
				var hashCode = -233066942;
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(First);
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Last);
				return hashCode;
			}
		}

		private static Dictionary<PersonName, bool> s_cache = new Dictionary<PersonName, bool>();

		public static string CacheFile
		{
			get { return Path.Combine(WikiCrawler.Configuration.DataDirectory, "nps_directory_cache.json"); }
		}

		static NPSDirectoryQuery()
		{
			if (File.Exists(CacheFile))
			{
				PersonName[] names = Newtonsoft.Json.JsonConvert.DeserializeObject<PersonName[]>(
					File.ReadAllText(CacheFile, Encoding.UTF8));
				foreach (PersonName name in names)
				{
					s_cache[name] = name.IsNPS;
				}
			}
		}

		public static void SaveOut()
		{
			File.WriteAllText(CacheFile,
				Newtonsoft.Json.JsonConvert.SerializeObject(s_cache.Keys, Newtonsoft.Json.Formatting.Indented),
				Encoding.UTF8);
		}

		/// <summary>
		/// Queries the directory and returns true if the specified person is an NPS employee.
		/// </summary>
		public static bool QueryDirectory(string firstName, string lastName)
		{
			bool cachedValue;
			if (s_cache.TryGetValue(new PersonName(firstName, lastName, false), out cachedValue))
			{
				return cachedValue;
			}

			Console.WriteLine("NPS Directory Query: " + lastName + ", " + firstName);

			WebRequest request = WebRequest.Create(string.Format(Endpoint, firstName, lastName));
			using (StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream()))
			{
				string text = reader.ReadToEnd();
				if (text.Contains("<b>N A M E </b>"))
				{
					s_cache[new PersonName(firstName, lastName, true)] = true;
					return true;
				}
				else if (text.Contains("<h4>Sorry, we're experiencing technical difficulties. Please try again later.</h4>"))
				{
					//TODO: switch to positive instead of negative check
					return false;
				}
				else
				{
					s_cache[new PersonName(firstName, lastName, false)] = false;
					return false;
				}
			}
		}
	}
}
