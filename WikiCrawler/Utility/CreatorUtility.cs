using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MediaWiki
{
	/// <summary>
	/// Caches useful information about authors and creators.
	/// </summary>
	public static class CreatorUtility
	{
		private static Dictionary<string, string> s_creatorHomecats = new Dictionary<string, string>();

		public static readonly Regex InlineCreatorTemplateRegex = new Regex(@"^{{\s*[Cc]reator\s*\|\s*[Ww]ikidata\s*=\s*(Q[0-9]+)\s*(?:|\s*[Oo]ption\s*=\s*)?}}$");
		public static readonly Regex AuthorLifespanRegex = new Regex(@"^([^\(\)]+)\s+\(?([0-9][0-9][0-9][0-9])[\-–—]([0-9][0-9][0-9][0-9])\)?$");

		/// <summary>
		/// If the string represents a creator template, returns that template's page title.
		/// </summary>
		public static PageTitle GetCreatorTemplate(string str)
		{
			//TODO: escape characters
			//TODO: nowiki
			//TODO: comments
			str = WikiUtils.TrimTemplate(str);

			if (str.Contains("{{"))
			{
				return PageTitle.Empty;
			}

			string[] paramSplit = str.Split('|');
			string[] templateSplit = paramSplit[0].Split(new char[] { ':' }, 2);
			if (templateSplit.Length == 2 && templateSplit[0].Equals("creator", StringComparison.InvariantCultureIgnoreCase))
			{
				return new PageTitle("Creator", templateSplit[1]);
			}
			else
			{
				return PageTitle.Empty;
			}
		}

		/// <summary>
		/// Returns true if the string represents a creator template.
		/// </summary>
		public static bool TryGetCreatorTemplate(string str, out PageTitle template)
		{
			//TODO: escape characters
			//TODO: nowiki
			//TODO: comments
			str = WikiUtils.TrimTemplate(str);

			if (str.Contains("{{"))
			{
				template = PageTitle.Empty;
				return false;
			}

			string[] paramSplit = str.Split('|');
			string[] templateSplit = paramSplit[0].Split(new char[] { ':' }, 2);
			if (templateSplit.Length == 2
				&& templateSplit[0].Equals("creator", StringComparison.InvariantCultureIgnoreCase)
				&& !string.IsNullOrWhiteSpace(templateSplit[1]))
			{
				template = new PageTitle("Creator", templateSplit[1]);
				return true;
			}
			else
			{
				template = PageTitle.Empty;
				return false;
			}
		}

		public static bool TryGetHomeCategory(string creator, out string homeCategory)
		{
			return s_creatorHomecats.TryGetValue(creator, out homeCategory);
		}

		public static void SetHomeCategory(string creator, string homeCategory)
		{
			s_creatorHomecats[creator] = homeCategory;
		}
	}
}