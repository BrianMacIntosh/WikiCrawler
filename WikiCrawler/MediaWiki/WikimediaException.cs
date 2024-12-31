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

		public WikimediaCodeException(Dictionary<string, object> error)
		{
			Code = (string)error["code"];
			Info = (string)error["info"];
		}

		public WikimediaCodeException(string code, string info)
		   : base()
		{
			Code = code;
			Info = info;
		}

		public override string Message => Code + ": " + Info;
	}
}
