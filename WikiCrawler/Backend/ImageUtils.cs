using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FreeImageAPI;
using System.IO;

public static class ImageUtils
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="filename"></param>
	/// <param name="percentage">The ratio of a row or column that must be solid white for crop to detect</param>
	/// <returns></returns>
	public static string AutoCropJpgSolidWhite(string filename, float percentage)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, filename, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP bitmap = FreeImage.ConvertTo32Bits(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		int left = width - 1;
		int right = 0;
		int bottom = height - 1;
		int top = 0;

		RGBQUAD color;
		RGBQUAD white = new RGBQUAD();
		white.uintValue = 0xFFFFFFFF;

		// determine top
		for (int y = height - 1; y >= 0; y--)
		{
			int goodPixels = 0;
			for (int x = 0; x < width; x++)
			{
				bool success = FreeImage.GetPixelColor(bitmap, (uint)x, (uint)y, out color);
				if (color == white)
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)width < percentage)
			{
				top = y;
				break;
			}
		}

		// determine bottom
		for (int y = 0; y < height; y++)
		{
			int goodPixels = 0;
			for (int x = 0; x < width; x++)
			{
				bool success = FreeImage.GetPixelColor(bitmap, (uint)x, (uint)y, out color);
				if (color == white)
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)width < percentage)
			{
				bottom = y;
				break;
			}
		}


		// determine right
		for (int x = width - 1; x >= 0; x--)
		{
			int goodPixels = 0;
			for (int y = 0; y < height; y++)
			{
				bool success = FreeImage.GetPixelColor(bitmap, (uint)x, (uint)y, out color);
				if (color == white)
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)height < percentage)
			{
				right = x;
				break;
			}
		}

		// determine left
		for (int x = 0; x < width; x++)
		{
			int goodPixels = 0;
			for (int y = 0; y < height; y++)
			{
				bool success = FreeImage.GetPixelColor(bitmap, (uint)x, (uint)y, out color);
				if (color == white)
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)height < percentage)
			{
				left = x;
				break;
			}
		}

		string tempfile = Path.Combine(
			Path.GetDirectoryName(filename),
			Path.GetFileNameWithoutExtension(filename) + "_crop" + Path.GetExtension(filename));

		FreeImage.JPEGCrop(filename, tempfile, left, height - top, right, height - bottom);

		return tempfile;
	}
}
