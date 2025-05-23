using MediaWiki;

namespace WikiCrawler
{
	/// <summary>
	/// Stores cached data about a particular author string.
	/// </summary>
	public class Creator
	{
		public string Author = "";
		public PageTitle Category = PageTitle.Empty;

		/// <summary>
		/// An override license template.
		/// </summary>
		public string LicenseTemplate = "";

		public int Usage = 0;
		public int UploadableUsage = 0;
		public int DeathYear = 9999;

		/// <summary>
		/// "Country of citizenship" QID.
		/// </summary>
		public string P27 = "";

		[Newtonsoft.Json.JsonIgnore]
		public bool IsEmpty
		{
			get
			{
				return string.IsNullOrEmpty(Author)
					&& Category.IsEmpty
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
			if (to.Category.IsEmpty)
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
			if (string.IsNullOrEmpty(to.P27))
			{
				to.P27 = from.P27;
			}
		}
	}
}
