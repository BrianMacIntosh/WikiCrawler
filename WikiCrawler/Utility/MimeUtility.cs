using System;

public static class MimeUtility
{
	/// <summary>
	/// Returns the file extension that corresponds to the specified MIME type.
	/// </summary>
	public static string GetExtensionFromMime(string mime)
	{
		switch (mime.ToLowerInvariant())
		{
			case "image/gif":  return ".gif";
			case "image/jpeg": return ".jpg";
			case "image/png": return ".png";
			case "image/bmp": return ".bmp";
			case "image/tiff": return ".tif";
			case "application/pdf": return ".pdf";
			default:
				throw new NotImplementedException("No extension for mime '" + mime + "'.");
		}
	}

	/// <summary>
	/// Returns the MIME type that corresponds to the specified file extension.
	/// </summary>
	public static string GetMimeFromExtension(string ext)
	{
		switch (ext.ToLowerInvariant())
		{
            case ".gif": return "image/gif";
            case ".jpg": return "image/jpeg";
			case ".jpeg": return "image/jpeg";
			case ".png": return "image/png";
            case ".bmp": return "image/bmp";
			case ".tif": return "image/tiff";
			case ".pdf": return "application/pdf";
			default:
				throw new NotImplementedException("No mime for extension '" + ext + "'.");
		}
	}
}
