using MediaWiki;

namespace Tasks.Commons
{
	public class LicenseHeaderTask : ReplaceInCategory
	{
		protected override bool UseCheckpoint => false;

		public LicenseHeaderTask()
			: base(new LicenseHeaderCleanup())
		{

		}

		public override PageTitle GetCategory()
		{
			return new PageTitle(PageTitle.NS_Category, "Hamlet Fifth Quarto");
		}
	}

	/// <summary>
	/// Cleans up problems with {{int:license-header}} headers.
	/// </summary>
	public class LicenseHeaderCleanup : BaseReplacement
	{
		//TODO: handle whitespace
		private const string licenseHeader = "=={{int:license-header}}==";

		public override bool DoReplacement(Article article)
		{
			bool replaced = false;
			int clearedIndex = 0;

			string text = article.revisions[0].text;

			while (true)
			{
				int index = text.IndexOf(licenseHeader, clearedIndex);
				if (index < 0)
				{
					break;
				}

				int afterHeader = index + licenseHeader.Length;
				while (afterHeader < text.Length && char.IsWhiteSpace(text[afterHeader]))
				{
					afterHeader++;
				}

				// must start a line
				//TODO: handle preceeding whitespace
				if (index == 0 || text[index - 1] != '\n')
				{
					ConsoleUtility.WriteLine(System.ConsoleColor.Green, "  Adding line return");
					text = text.Substring(0, index) + "\n" + text.Substring(index);
					afterHeader++;
					index++;
					replaced = true;
				}

				// must preceed a license
				if (!text.MatchAt("{{", afterHeader))
				{
					ConsoleUtility.WriteLine(System.ConsoleColor.Green, "  Removing dangling header");
					text = text.Remove(index, licenseHeader.Length);
					while (char.IsWhiteSpace(text[index]))
					{
						text = text.Remove(index, 1);
					}
					replaced = true;
					continue;
				}

				clearedIndex = index + 1;
			}

			if (replaced)
			{
				article.revisions[0].text = text;
				article.Changes.Add("cleaning up license-headers");
				article.Dirty = true;
			}
			return replaced;
		}
	}
}
