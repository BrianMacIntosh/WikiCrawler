using MediaWiki;

namespace Tasks
{
	/// <summary>
	/// Base class for an object that performs text replacement on a page.
	/// </summary>
	public abstract class BaseReplacement
	{
		public virtual bool UseHeartbeat
		{
			get { return false; }
		}

		public HeartbeatData Heartbeat;

		/// <summary>
		/// Performs the replacement on the specified article.
		/// </summary>
		/// <remarks>Should mark the article as dirty and add a Changes comment to it.</remarks>
		/// <returns>True if a replacement was made.</returns>
		public abstract bool DoReplacement(Article article);

		/// <summary>
		/// If the operation has any data to save out, does so.
		/// </summary>
		public virtual void SaveOut()
		{

		}
	}
}
