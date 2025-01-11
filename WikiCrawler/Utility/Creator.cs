namespace WikiCrawler
{
	/// <summary>
	/// Stores cached data about a particular author string.
	/// </summary>
	public class Creator
	{
		public string Author = "";
		public string Category = "";

		/// <summary>
		/// An override license template.
		/// </summary>
		public string LicenseTemplate = "";

		public int Usage = 0;
		public int UploadableUsage = 0;
		public int DeathYear = 9999;

		[Newtonsoft.Json.JsonIgnore]
		public bool IsEmpty
		{
			get
			{
				return string.IsNullOrEmpty(Author)
					&& string.IsNullOrEmpty(Category)
					&& string.IsNullOrEmpty(LicenseTemplate)
					&& DeathYear == 9999;
			}
		}

		public static void Merge(Creator from, Creator to)
		{
			if (string.IsNullOrEmpty(to.Author))
			{
				to.Author = from.Author;
			}
			if (string.IsNullOrEmpty(to.Category))
			{
				to.Category = from.Category;
			}
			if (string.IsNullOrEmpty(to.LicenseTemplate))
			{
				to.LicenseTemplate = from.LicenseTemplate;
			}
			if (to.DeathYear == 9999)
			{
				to.DeathYear = from.DeathYear;
			}
		}
	}
}
