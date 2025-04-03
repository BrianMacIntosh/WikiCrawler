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
		public RevertContribs()
		{
			Parameters["StartTime"] = "2018-09-22 16:00:00"; //very generous
			Parameters["EndTime"] = "2018-09-22 17:20:00";
			Parameters["User"] = "BMacZero";
			Parameters["Comment"] = "replace city with recognized name - Doing 1 replacements.";
		}

		public override void Execute()
		{
			Api Api = GlobalAPIs.Commons;
			
			string startTime = Parameters["StartTime"];
			string endTime = Parameters["EndTime"];

			List<int> badrevs = File.ReadAllLines("E:/temp.txt")
				.Where(l => !string.IsNullOrEmpty(l))
				.Select(l => int.Parse(l))
				.ToList();

			try
			{
				foreach (Contribution contrib in Api.GetContributions(Parameters["User"], endTime, startTime))
				{
					if (contrib.comment == Parameters["Comment"]
						&& !badrevs.Contains(contrib.revid))
					{
						Console.WriteLine(contrib.title);
						badrevs.Add(contrib.revid);
						Api.UndoRevision(contrib.pageid, contrib.revid, "", true);
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