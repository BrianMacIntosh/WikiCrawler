using FreeImageAPI;
using System;
using System.IO;

public static class ImageUtils
{
	[Flags]
	public enum Side
	{
		Top = 1 << 0,
		Left = 1 << 1,
		Right = 1 << 2,
		Bottom = 1 << 3,
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="sourceFile"></param>
	/// <returns></returns>
	public static void AutoCropJpgSolid(string sourceFile, string outFile, uint color, float percentage = 1f)
	{
		RGBQUAD rgbColor = new RGBQUAD();
		rgbColor.uintValue = color;
		AutoCropJpg(sourceFile, outFile, rgbColor, percentage, 0, Side.Top | Side.Left | Side.Right | Side.Bottom);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="sourceFile"></param>
	/// <param name="percentage">The ratio of a row or column that must be solid white for crop to detect</param>
	/// <returns></returns>
	public static void CropJpgBottom(string sourceFile, string outFile, int amt)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, sourceFile, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP bitmap = FreeImage.ConvertTo32Bits(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		FreeImage.JPEGCrop(sourceFile, outFile, 0, 0, width - 1, height - amt - 1);
	}

	/// <summary>
	/// 
	/// </summary>
	public static void CropUwashWatermark(string sourceFile, string outFile)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, sourceFile, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP bitmap = FreeImage.ConvertTo32Bits(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		int bottomBorder = GetBottomBorder(bitmap, 0xffffffff, 0.99f, 12);
		if (bottomBorder >= 4 && bottomBorder <= 9)
		{
			FreeImage.JPEGCrop(sourceFile, outFile, 0, 0, width, height - 30);
		}
		else
		{
			if (File.Exists(outFile)) File.Delete(outFile);
			File.Copy(sourceFile, outFile);
		}
	}

	public static void AutoCropJpg(string sourceFile, string outFile, RGBQUAD target, float percentage, int leeway)
	{
		AutoCropJpg(sourceFile, outFile, target, percentage, leeway, Side.Left | Side.Right | Side.Top | Side.Bottom);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="sourceFile"></param>
	/// <param name="outFile"></param>
	/// <param name="percentage">The ratio of a row or column that must be solid white for crop to detect</param>
	/// <param name="leeway">Difference allowed in each color channel to still detect as a match against target.</param>
	/// <param name="sides"></param>
	public static void AutoCropJpg(string sourceFile, string outFile, RGBQUAD target, float percentage, int leeway, Side sides)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, sourceFile, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP bitmap = FreeImage.ConvertTo32Bits(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		int left = width;
		int right = 0;
		int bottom = 0;
		int top = height;
		
		// determine top
		if ((sides & Side.Top) != 0)
		{
			top = GetTopBorder(bitmap, target, percentage, leeway);
		}

		// determine bottom
		if ((sides & Side.Bottom) != 0)
		{
			bottom = GetBottomBorder(bitmap, target, percentage, leeway);
		}

		// determine right
		if ((sides & Side.Right) != 0)
		{
			right = GetRightBorder(bitmap, target, percentage, leeway);
		}

		// determine left
		if ((sides & Side.Left) != 0)
		{
			left = GetLeftBorder(bitmap, target, percentage, leeway);
		}

		if (!FreeImage.JPEGCrop(sourceFile, outFile, left, height - top, right, height - bottom))
		{
			throw new Exception("Crop failed");
		}
	}

	private static int GetTopBorder(FIBITMAP image, RGBQUAD target, float percentage, int leeway)
	{
		int width = (int)FreeImage.GetWidth(image);
		int height = (int)FreeImage.GetHeight(image);
		for (int y = height - 1; y >= 0; y--)
		{
			int goodPixels = 0;
			for (int x = 0; x < width; x++)
			{
				RGBQUAD color;
				bool success = FreeImage.GetPixelColor(image, (uint)x, (uint)y, out color);
				if (IsAlmost(color, target, leeway))
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)width < percentage)
			{
				return y + 1;
			}
		}
		return 0;
	}

	private static int GetBottomBorder(FIBITMAP image, RGBQUAD target, float percentage, int leeway)
	{
		int width = (int)FreeImage.GetWidth(image);
		int height = (int)FreeImage.GetHeight(image);
		for (int y = 0; y < height; y++)
		{
			int goodPixels = 0;
			for (int x = 0; x < width; x++)
			{
				RGBQUAD color;
				bool success = FreeImage.GetPixelColor(image, (uint)x, (uint)y, out color);
				if (IsAlmost(color, target, leeway))
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)width < percentage)
			{
				return y;
			}
		}
		return height - 1;
	}

	private static int GetRightBorder(FIBITMAP image, RGBQUAD target, float percentage, int leeway)
	{
		int width = (int)FreeImage.GetWidth(image);
		int height = (int)FreeImage.GetHeight(image);
		for (int x = width - 1; x >= 0; x--)
		{
			int goodPixels = 0;
			for (int y = 0; y < height; y++)
			{
				RGBQUAD color;
				bool success = FreeImage.GetPixelColor(image, (uint)x, (uint)y, out color);
				if (IsAlmost(color, target, leeway))
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)height < percentage)
			{
				return x + 1;
			}
		}
		return 0;
	}

	private static int GetLeftBorder(FIBITMAP image, RGBQUAD target, float percentage, int leeway)
	{
		int width = (int)FreeImage.GetWidth(image);
		int height = (int)FreeImage.GetHeight(image);
		for (int x = 0; x < width; x++)
		{
			int goodPixels = 0;
			for (int y = 0; y < height; y++)
			{
				RGBQUAD color;
				bool success = FreeImage.GetPixelColor(image, (uint)x, (uint)y, out color);
				if (IsAlmost(color, target, leeway))
				{
					goodPixels++;
				}
			}
			if (goodPixels / (float)height < percentage)
			{
				return x;
			}
		}
		return width - 1;
	}

	private static bool IsAlmost(RGBQUAD a, RGBQUAD b, int leeway)
	{
		return Math.Abs(a.rgbRed - b.rgbRed) <= leeway
			&& Math.Abs(a.rgbBlue - b.rgbBlue) <= leeway
			&& Math.Abs(a.rgbGreen - b.rgbGreen) <= leeway;
	}

	private static bool Similar(this RGBQUAD color, RGBQUAD other, float leeway)
	{
		throw new NotImplementedException();
	}
}
