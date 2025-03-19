using MediaWiki;
using System;

namespace Tasks
{
	/// <summary>
	/// Replacement task that performs multiple replacements in one edit.
	/// </summary>
	public class CompoundReplacementTask : BaseReplacement
	{
		private BaseReplacement[] m_tasks;

		public CompoundReplacementTask(params BaseReplacement[] tasks)
		{
			m_tasks = tasks;
		}

		public override bool DoReplacement(Article article)
		{
			bool bAnyChange = false;

			foreach (BaseReplacement task in m_tasks)
			{
				ConsoleUtility.WriteLine(ConsoleColor.White, "{0} on '{1}'", task.GetType().Name, article.title);

				bool bChange = task.DoReplacement(article);
				bAnyChange = bAnyChange || bChange;
			}

            return bAnyChange;
		}

		public override void SaveOut()
		{
			base.SaveOut();

			foreach (BaseReplacement task in m_tasks)
			{
				task.SaveOut();
			}
		}
	}
}
