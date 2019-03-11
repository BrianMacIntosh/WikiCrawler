using System;

namespace Wikimedia
{
	public class WikimediaException : Exception
	{
		public WikimediaException()
			: base()
		{

		}

		public WikimediaException(string msg)
		   : base(msg)
		{

		}
	}
}
