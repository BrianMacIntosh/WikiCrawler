using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaWiki;

namespace Tasks
{
	/// <summary>
	/// Reverts a range of contributions.
	/// </summary>
	public class RevertContribs : BaseTask
	{
		public override void Execute()
		{
			Uri commons = new Uri("https://commons.wikimedia.org/");
			EasyWeb.SetDelayForDomain(commons, 0.1f);
			Api Api = new Api(commons);
			Api.AutoLogIn();

			string startTime = "2018-09-22 16:00:00"; //very generous
			string endTime = "2018-09-22 17:20:00";

			List<int> badrevs = File.ReadAllLines("E:/temp.txt")
				.Where(l => !string.IsNullOrEmpty(l))
				.Select(l => int.Parse(l))
				.ToList();

			try
			{
				foreach (Contribution contrib in Api.GetContributions("BMacZero", endTime, startTime))
				{
					if (contrib.comment == "replace city with recognized name - Doing 1 replacements."
						&& !badrevs.Contains(contrib.revid))
					{
						Console.WriteLine(contrib.title);
						badrevs.Add(contrib.revid);
						Api.UndoRevision(contrib.pageid, contrib.revid, true);
					}
				}
			}
			finally
			{
				File.WriteAllLines("E:/temp.txt", badrevs.Select(i => i.ToString()).ToArray());
			}
		}
	}
}