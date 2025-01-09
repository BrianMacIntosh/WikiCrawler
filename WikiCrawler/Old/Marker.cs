
class Marker
{
    private string matches;
    private bool caseInsense = false;

    public int Length { get { return matches.Length; } }
    public string Contents { get { return matches; } }

    public Marker(string matches, bool caseInsense = false)
    {
        this.matches = matches;
        this.caseInsense = caseInsense;
    }

	/// <summary>
	/// 
	/// </summary>
	/// <returns>True if the entire marker was matched.</returns>
	public bool MatchAgainst(string str, int position)
    {
        int currentIndex = 0;
        for (int i = position; i < str.Length; ++i)
        {
            char currentChar = str[i];
            if (currentChar == matches[currentIndex]
                || (caseInsense && Lower(currentChar) == Lower(matches[currentIndex])))
            {
                currentIndex++;
                if (currentIndex >= matches.Length)
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    [System.Obsolete()]
    public bool MatchAgainst(char c)
	{
        return false;
	}

    [System.Obsolete()]
    public void Reset()
	{

	}

    private static char Lower(char c)
    {
        if (c >= 'A' && c <= 'Z')
            return (char)(c | 32);
        else
            return c;
    }
}