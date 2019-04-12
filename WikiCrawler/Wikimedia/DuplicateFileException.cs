using System.Collections.Generic;
using System.Linq;

namespace Wikimedia
{
	public class DuplicateFileException : WikimediaException
	{
		public readonly string FileName;

		public readonly object[] Duplicates;

		public bool IsSelfDuplicate
		{
			get { return DuplicateTitles.Contains(FileName); }
		}

		public DuplicateFileException(string fileName, object[] duplicates)
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

		public IEnumerable<string> DuplicateTitles
		{
			get
			{
				foreach (object dupeObj in Duplicates)
				{
					yield return (string)((Dictionary<string, object>)Duplicates[0])["title"];
				}
			}
		}
	}
}
