using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tasks
{
	/// <summary>
	/// Produces Commons Delinker move commands for a large list of categories.
	/// </summary>
	public class RequestMassCatMove : BaseTask
	{
		public override void Execute()
		{
			List<string> output = new List<string>();

			foreach (string catname in File.ReadAllLines("C:/Users/Brian/Desktop/revival3.csv", Encoding.Default))
			{
				string catnameReal = catname.Replace('_', ' ');

				Console.WriteLine(catnameReal);

				Article catpage = GlobalAPIs.Commons.GetPage("Category:" + catnameReal);

				EasyWeb.crawlDelay = 0.1f;

				if (!catpage.missing && catpage.revisions != null && !catpage.revisions[0].text.Contains("{{Category redirect|"))
				{
					output.Add("{{move cat|" + catnameReal + "|" + catnameReal.Replace("revival", "Revival") +
						"|3=[[Commons:Categories for discussion/2012/01/Revival architectural styles]]|user=BMacZero}}");
				}
			}

			File.WriteAllLines("C:/Users/Brian/Desktop/requests3.txt", output.ToArray(), Encoding.Default);
		}
	}
}
