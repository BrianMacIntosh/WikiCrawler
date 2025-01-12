using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tasks
{
	/// <summary>
	/// Base class for a task the bot can run.
	/// </summary>
	public abstract class BaseTask
	{
		/// <summary>
		/// Runs the task synchronously.
		/// </summary>
		public abstract void Execute();
	}
}
