using FreeImageAPI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public struct FreeImageTag
{
	public string Key;

	public string Description;

	public FREE_IMAGE_MDTYPE Type;

	public uint Length;

	public IntPtr Value;

	public string StringValue;

	public object GetValue()
	{
		switch (Type)
		{
			case FREE_IMAGE_MDTYPE.FIDT_ASCII:
				return Marshal.PtrToStringAnsi(Value, (int)Length);
			case FREE_IMAGE_MDTYPE.FIDT_SHORT:
				return Marshal.ReadInt16(Value);
			case FREE_IMAGE_MDTYPE.FIDT_LONG:
				return Marshal.ReadInt32(Value);
			default:
				return "";
		}
	}
}

public static class ImageUtility
{
	/// <summary>
	/// Returns a dictionary of EXIF metadata from the specified file.
	/// </summary>
	public static Dictionary<string, FreeImageTag> GetExif(string sourceFile)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, sourceFile, FREE_IMAGE_LOAD_FLAGS.DEFAULT);

		Dictionary<string, FreeImageTag> results = new Dictionary<string, FreeImageTag>();

		FITAG tag;
		FIMETADATA handle = FreeImage.FindFirstMetadata(FREE_IMAGE_MDMODEL.FIMD_EXIF_EXIF, raw, out tag);
		while (!tag.IsNull)
		{
			FreeImageTag nicetag = new FreeImageTag();
			nicetag.Key = FreeImage.GetTagKey(tag);
			nicetag.Description = FreeImage.GetTagDescription(tag);
			nicetag.Type = FreeImage.GetTagType(tag);
			nicetag.Length = FreeImage.GetTagLength(tag);
			nicetag.Value = FreeImage.GetTagValue(tag);
			nicetag.StringValue = nicetag.GetValue().ToString();
			results[nicetag.Key] = nicetag;
			if (!FreeImage.FindNextMetadata(handle, out tag))
			{
				break;
			}
		}

		return results;
	}
}
