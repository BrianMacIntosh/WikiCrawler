public struct StringSpan
{
	public int start;

	public int end;

	public int Length
	{
		get { return end - start + 1; }
	}

	public bool IsValid
	{
		get { return start >= 0 && end >= 0 && end >= start; }
	}

	public static readonly StringSpan Empty = new StringSpan(-1, -1);

	public StringSpan(int inStart, int inEnd)
	{
		start = inStart;
		end = inEnd;
	}
}
