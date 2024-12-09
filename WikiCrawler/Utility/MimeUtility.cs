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
			case "image/webp": return ".webp";
			case "application/pdf": return ".pdf";
			case "video/mp4": return ".mp4";
			case "video/mpeg": return ".mpg";
			case "audio/wav": return ".wav";
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
			case ".jfif": return "image/jpeg";
			case ".png": return "image/png";
            case ".bmp": return "image/bmp";
			case ".tif": return "image/tiff";
			case ".tiff": return "image/tiff";
			case ".webp": return "image/webp";
			case ".pdf": return "application/pdf";
			case ".mp4": return "video/mp4";
			case ".mpg": return "video/mpeg";
			case ".mpeg": return "video/mpeg";
			case ".wav": return "audio/wav";
			default:
				throw new NotImplementedException("No mime for extension '" + ext + "'.");
		}
	}
}
