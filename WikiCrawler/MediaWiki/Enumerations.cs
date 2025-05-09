using System;

namespace MediaWiki
{
	public static class Limit
	{
		/// <summary>
		/// Use the maximum value.
		/// </summary>
		public const int Max = -2;

		/// <summary>
		/// The parameter was unspecified.
		/// </summary>
		public const int Unspecified = -1;
	}

	public static class Direction
	{
		public const string Newer = "newer";

		public const string Older = "older";
	}

	public static class Prop
	{
		/// <summary>
		/// List all categories the pages belong to.
		/// </summary>
		public const string categories = "categories";

		/// <summary>
		/// Returns information about the given categories.
		/// </summary>
		public const string categoryinfo = "categoryinfo";

		/// <summary>
		/// Get the list of logged-in contributors and the count of anonymous contributors to a page.
		/// </summary>
		public const string contributors = "contributors";

		/// <summary>
		/// Get deleted revision information.
		/// </summary>
		public const string deletedrevisions = "deletedrevisions";

		/// <summary>
		/// List duplicates of the given files.
		/// </summary>
		public const string duplicatefiles = "duplicatefiles";

		/// <summary>
		/// Gets a list of all external links on the provided pages.
		/// </summary>
		public const string extlinks = "extlinks";

		/// <summary>
		/// Find all pages that use the given files.
		/// </summary>
		public const string fileusage = "fileusage";

		/// <summary>
		/// Returns file information and upload history.
		/// </summary>
		public const string imageinfo = "imageinfo";

		/// <summary>
		/// Returns all files contained on the given pages.
		/// </summary>
		public const string images = "images";

		/// <summary>
		/// Get basic page information.
		/// </summary>
		public const string info = "info";

		/// <summary>
		/// Returns all interwiki links from the given pages.
		/// </summary>
		public const string iwlinks = "iwlinks";

		/// <summary>
		/// Gets a list of all language links from the provided pages to other languages.
		/// </summary>
		public const string langlinks = "langlinks";

		/// <summary>
		/// Returns all links from the given pages.
		/// </summary>
		public const string links = "links";

		/// <summary>
		/// Find all pages that link to the given pages.
		/// </summary>
		public const string linkshere = "linkshere";

		/// <summary>
		/// Get various properties defined in the page content.
		/// </summary>
		public const string pageprops = "pageprops";

		/// <summary>
		/// Returns all redirects to the given pages.
		/// </summary>
		public const string redirects = "redirects";

		/// <summary>
		/// Get revision information.
		/// </summary>
		public const string revisions = "revisions";

		/// <summary>
		/// Returns file information for stashed files.
		/// </summary>
		public const string stashimageinfo = "stashimageinfo";

		/// <summary>
		/// Gets a list of all pages (typically templates) transcluded in the provided pages
		/// </summary>
		public const string templates = "templates";

		/// <summary>
		/// Find all pages that transclude the given pages.
		/// </summary>
		public const string transcludedin = "transcludedin";
	}

	public static class IIProp
	{
		/// <summary>
		/// Adds timestamp for the uploaded version.
		/// </summary>
		public const string timestamp = "timestamp";

		/// <summary>
		/// Adds the user who uploaded each file version.
		/// </summary>
		public const string user = "user";

		/// <summary>
		/// Add the ID of the user that uploaded each file version.
		/// </summary>
		public const string userid = "userid";

		/// <summary>
		/// Comment on the version.
		/// </summary>
		public const string comment = "comment";

		/// <summary>
		/// Parse the comment on the version.
		/// </summary>
		public const string parsedcomment = "parsedcomment";

		/// <summary>
		/// Adds the canonical title of the file.
		/// </summary>
		public const string canonicaltitle = "canonicaltitle";

		/// <summary>
		/// Gives URL to the file and the description page.
		/// </summary>
		public const string url = "url";

		/// <summary>
		/// Adds the size of the file in bytes and the height, width and page count (if applicable).
		/// </summary>
		public const string size = "size";

		/// <summary>
		/// Alias for size.
		/// </summary>
		public const string dimensions = "dimensions";

		/// <summary>
		/// Adds SHA-1 hash for the file.
		/// </summary>
		public const string sha1 = "sha1";

		/// <summary>
		/// Adds MIME type of the file.
		/// </summary>
		public const string mime = "mime";

		/// <summary>
		/// Adds MIME type of the image thumbnail (requires url and param iiurlwidth).
		/// </summary>
		public const string thumbmime = "thumbmime";

		/// <summary>
		/// Adds the media type of the file.
		/// </summary>
		public const string mediatype = "mediatype";

		/// <summary>
		/// Lists Exif metadata for the version of the file.
		/// </summary>
		public const string metadata = "metadata";

		/// <summary>
		/// Lists file format generic metadata for the version of the file.
		/// </summary>
		public const string commonmetadata = "commonmetadata";

		/// <summary>
		/// Lists formatted metadata combined from multiple sources.Results are HTML formatted.
		/// </summary>
		public const string extmetadata = "extmetadata";

		/// <summary>
		/// Adds the filename of the archive version for non-latest versions.
		/// </summary>
		public const string archivename = "archivename";

		/// <summary>
		/// Adds the bit depth of the version.
		/// </summary>
		public const string bitdepth = "bitdepth";

		/// <summary>
		/// Used by the Special:Upload page to get information about an existing file. Not intended for use outside MediaWiki core.
		/// </summary>
		public const string uploadwarning = "uploadwarning";

		/// <summary>
		/// Adds whether the file is on the [[MediaWiki:Bad image list]].
		/// </summary>
		public const string badfile = "badfile";
	}

	public static class RVProp
	{
		/// <summary>
		/// The ID of the revision.
		/// </summary>
		public const string ids = "ids";

		/// <summary>
		/// Revision flags (minor).
		/// </summary>
		public const string flags = "flags";

		/// <summary>
		/// The timestamp of the revision.
		/// </summary>
		public const string timestamp = "timestamp";

		/// <summary>
		/// User that made the revision.
		/// </summary>
		public const string user = "user";

		/// <summary>
		/// User ID of the revision creator.
		/// </summary>
		public const string userid = "userid";

		/// <summary>
		/// Length (bytes) of the revision.
		/// </summary>
		public const string size = "size";

		/// <summary>
		/// Length (bytes) of each revision slot.
		/// </summary>
		public const string slotsize = "slotsize";

		/// <summary>
		/// SHA-1 (base 16) of the revision.
		/// </summary>
		public const string sha1 = "sha1";

		/// <summary>
		/// SHA-1 (base 16) of each revision slot.
		/// </summary>
		public const string slotsha1 = "slotsha1";

		/// <summary>
		/// Content model ID of each revision slot.
		/// </summary>
		public const string contentmodel = "contentmodel";

		/// <summary>
		/// Comment by the user for the revision.
		/// </summary>
		public const string comment = "comment";

		/// <summary>
		/// Parsed comment by the user for the revision.
		/// </summary>
		public const string parsedcomment = "parsedcomment";

		/// <summary>
		/// Content of each revision slot.
		/// </summary>
		public const string content = "content";

		/// <summary>
		/// Tags for the revision.
		/// </summary>
		public const string tags = "tags";

		/// <summary>
		/// List content slot roles that exist in the revision.
		/// </summary>
		public const string roles = "roles";

		/// <summary>
		/// The XML parse tree of revision content (requires content model 'wikitext').
		/// </summary>
		[Obsolete("Use action=expandtemplates or action=parse instead.")]
		public const string parsetree = "parsetree";
	}

	/// <summary>
	/// Which type of category members to include. Ignored when 'cmsort=timestamp' is set.
	/// </summary>
	public static class CMType
	{
		public const string page = "page";
		public const string subcat = "subcat";
		public const string file = "file";
	}
	
	public static class WBProp
	{
		/// <summary>
		/// 
		/// </summary>
		public const string info = "info";

		/// <summary>
		/// 
		/// </summary>
		public const string sitelinks = "sitelinks";

		/// <summary>
		/// 
		/// </summary>
		public const string sitelinksurls = "sitelinks/urls";

		/// <summary>
		/// 
		/// </summary>
		public const string aliases = "aliases";

		/// <summary>
		/// 
		/// </summary>
		public const string labels = "labels";

		/// <summary>
		/// 
		/// </summary>
		public const string descriptions = "descriptions";

		/// <summary>
		/// 
		/// </summary>
		public const string claims = "claims";

		/// <summary>
		/// 
		/// </summary>
		public const string datatype = "datatype";
	}
}
