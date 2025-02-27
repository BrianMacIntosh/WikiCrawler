using MediaWiki;
using System;

namespace Tasks.Commons
{
	public class PdArtYorckProjectUsage : BaseTask
	{
		public override void Execute()
		{
			var rep = new PdArtYorckProjectReplacement();
			int count = 0;
			foreach (Article article in GlobalAPIs.Commons.FetchArticles(GlobalAPIs.Commons.Search("insource:PD-Art-YorckProject", srnamespace: Api.BuildNamespaceList(Namespace.File), srwhat: "text")))
			{
				count++;
				Console.WriteLine("(" + count + ") " + article.title);
				rep.DoReplacement(article);
			}
		}
	}

	public class PdArtYorckProjectReplacement : BaseReplacement
	{
		public override bool DoReplacement(Article article)
		{
			string pdarty = WikiUtils.ExtractTemplate(article.revisions[0].text, "PD-Art-YorckProject");
			bool yorck = WikiUtils.HasTemplate(article.revisions[0].text, "Yorck");

			if (!string.IsNullOrEmpty(pdarty) && pdarty != "PD-Art-YorckProject")
			{
				return false;
			}
			else if (yorck)
			{
				return false;
			}
			else
			{
				return false;
			}
		}
	}
}
