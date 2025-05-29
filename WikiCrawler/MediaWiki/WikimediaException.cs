using System;
using System.Collections.Generic;

namespace MediaWiki
{
	public class WikimediaException : Exception
	{
		public WikimediaException()
			: base()
		{

		}

		public WikimediaException(string message)
		   : base(message)
		{

		}
	}

	public class WikimediaCodeException : WikimediaException
	{
		public string Code;

		public string Info;

		public object[] Details;

		public WikimediaCodeException(Dictionary<string, object> error)
		{
			Code = (string)error["code"];
			Info = (string)error["info"];
			if (error.TryGetValue("details", out object details))
			{
				Details = (object[])details;
			}
		}

		public WikimediaCodeException(string code, string info)
		   : base()
		{
			Code = code;
			Info = info;
			Details = null;
		}

		public override string Message => Code + ": " + Info;
	}
}
