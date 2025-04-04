using MediaWiki;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tasks
{
	/// <summary>
	/// Produces a summary of contribution messages.
	/// </summary>
	public class ContribsSummary : BaseTask
	{
		public ContribsSummary()
		{
			Parameters["StartTime"] = "2025-01-17 00:00:00";
			Parameters["EndTime"] = "now";
			Parameters["User"] = "BMacZeroBot";
		}

		public override void Execute()
		{
			Dictionary<string, int> summaries = new Dictionary<string, int>();

			Api Api = GlobalAPIs.Commons;

			string startTime = Parameters["StartTime"];
			string endTime = Parameters["EndTime"];

			try
			{
				foreach (Contribution contrib in Api.GetContributions(Parameters["User"], endTime, startTime))
				{
					if (!summaries.TryGetValue(contrib.comment, out int count))
					{
						summaries[contrib.comment] = 1;
					}
					else
					{
						summaries[contrib.comment] = count + 1;
					}
					Console.WriteLine(contrib.timestamp);
				}
			}
			finally
			{
				File.WriteAllLines("E:/contribs_summary.csv", summaries.Select(i => i.Value + ",\"" + i.Key + "\"").ToArray());
			}
		}
	}
}
