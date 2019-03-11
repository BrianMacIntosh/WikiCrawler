using System;
using System.Collections.Generic;

namespace Wikimedia
{
	public class DuplicateFileException : WikimediaException
	{
		public readonly string FileName;

		public readonly object[] Duplicates;

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
	}
}
