using System.Collections.Generic;
using System.Linq;

namespace MediaWiki
{
	public class DuplicateFileException : WikimediaException
	{
		public readonly PageTitle FileName;

		public readonly object[] Duplicates;

		public bool IsSelfDuplicate
		{
			get { return DuplicateTitles.Contains(FileName); }
		}

		public DuplicateFileException(PageTitle fileName, object[] duplicates)
		{
			FileName = fileName;
			Duplicates = duplicates;
		}

		public override string Message
		{
			get
			{
				string dupe = (string)((Dictionary<string, object>)Duplicates[0])["title"];
				return "'" + FileName + "' is a duplicate of '" + dupe + "'.";
			}
		}

		public IEnumerable<PageTitle> DuplicateTitles
		{
			get
			{
				foreach (object dupeObj in Duplicates)
				{
					yield return PageTitle.Parse((string)((Dictionary<string, object>)Duplicates[0])["title"]);
				}
			}
		}
	}
}
