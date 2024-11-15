using System;

namespace NPGallery
{
	public static class NPGallery
	{
		public static Guid StringToKey(string str)
		{
			// the GUIDs from NPGallery are sometimes missing the last hyphen
			return new Guid(str.Replace("-", ""));
		}
	}
}
