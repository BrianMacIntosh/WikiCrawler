using MediaWiki;

namespace Tasks
{
	/// <summary>
	/// Replacement task that performs multiple replacements in one edit.
	/// </summary>
	public class CompoundReplacementTask : BaseReplacement
	{
		private BaseReplacement[] m_tasks;

		/// <summary>
		/// Tasks that will only run if the primary tasks make an edit.
		/// </summary>
		private BaseReplacement[] m_conditionalTasks;

		public CompoundReplacementTask(params BaseReplacement[] tasks)
		{
			m_tasks = tasks;
		}

		public CompoundReplacementTask Conditional(params BaseReplacement[] conditionalTasks)
		{
			m_conditionalTasks = conditionalTasks;
			return this;
		}

		public override bool DoReplacement(Article article)
		{
			bool bAnyChange = false;

			foreach (BaseReplacement task in m_tasks)
			{
				bool bChange = task.DoReplacement(article);
				bAnyChange = bAnyChange || bChange;
			}

            if (bAnyChange)
            {
				foreach (BaseReplacement task in m_conditionalTasks)
				{
					task.DoReplacement(article);
				}
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
