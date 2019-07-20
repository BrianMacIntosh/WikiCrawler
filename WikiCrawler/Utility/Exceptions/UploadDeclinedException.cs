using System;

/// <summary>
/// Thrown when a file was not suitable for upload (no scope, etc.)
/// </summary>
public class UploadDeclinedException : Exception
{
	public override string Message
	{
		get
		{
			if (string.IsNullOrEmpty(Reason))
			{
				return "Upload declined";
			}
			else
			{
				return "Upload declined (" + Reason + ")";
			}
		}
	}

	public readonly string Reason;

	public UploadDeclinedException()
	{

	}

	public UploadDeclinedException(string reason)
	{
		Reason = reason;
	}
}
