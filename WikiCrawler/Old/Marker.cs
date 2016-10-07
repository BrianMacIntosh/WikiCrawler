
[System.Obsolete("This class is a lie.")]
class Marker
{
    private string matches;
    private int currentIndex = 0;
    private bool caseInsense = false;

    public int Length { get { return matches.Length; } }
    public string Contents { get { return matches; } }

    public Marker(string matches, bool caseInsense = false)
    {
        this.matches = matches;
        this.caseInsense = caseInsense;
    }

    //Returns true if the entire marker was matched
    public bool MatchAgainst(char c)
    {
		if (c == matches[currentIndex]
			|| (caseInsense && Lower(c) == Lower(matches[currentIndex])))
		{
			currentIndex++;
			if (currentIndex >= matches.Length)
			{
				currentIndex = 0;
				return true;
			}
		}
		else
		{
			currentIndex = 0;
		}
        return false;
    }

    public void Reset()
    {
        currentIndex = 0;
    }

    private static char Lower(char c)
    {
        if (c >= 'A' && c <= 'Z')
            return (char)(c | 32);
        else
            return c;
    }
}