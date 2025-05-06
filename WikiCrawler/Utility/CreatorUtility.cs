using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MediaWiki
{
	public struct CreatorTemplate
	{
		public PageTitle Template;
		public string Option;

		public bool IsEmpty { get { return Template.IsEmpty; } }
	}

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
		public static CreatorTemplate GetCreatorTemplate(string str)
		{
			TryGetCreatorTemplate(str, out CreatorTemplate creatorTemplate);
			return creatorTemplate;
		}

		/// <summary>
		/// Returns true if the string represents a creator template.
		/// </summary>
		public static bool TryGetCreatorTemplate(string str, out CreatorTemplate template)
		{
			//TODO: escape characters
			//TODO: nowiki
			//TODO: comments

			bool treatAsTemplate = str.Contains("{{");

			string templateName = treatAsTemplate ? WikiUtils.GetOnlyTemplateName(str) : str;
			if (string.IsNullOrEmpty(templateName))
			{
				template = new CreatorTemplate();
				return false;
			}

			PageTitle title = PageTitle.Parse(templateName);
			if (title.IsEmpty 
				|| !title.IsNamespace("creator")
				|| string.IsNullOrWhiteSpace(title.Name))
			{
				template = new CreatorTemplate();
				return false;
			}

			template = new CreatorTemplate() { Template = title };

			if (treatAsTemplate)
			{
				string templateText = WikiUtils.ExtractTemplate(str, templateName);
				template.Option = WikiUtils.GetTemplateParameter(1, templateText);
			}

			return true;
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