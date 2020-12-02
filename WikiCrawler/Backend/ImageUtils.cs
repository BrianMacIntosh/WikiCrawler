using FreeImageAPI;
using System;
using System.IO;

public struct CropParams
{
	/// <summary>
	/// The sides to crop.
	/// </summary>
	public ImageUtils.Side Sides;

	/// <summary>
	/// If set, all the specified sides must be cropped for the crop to be accepted.
	/// </summary>
	public bool RequireAllSides;

	public CropParams(ImageUtils.Side sides, bool requireAllSides)
	{
		Sides = sides;
		RequireAllSides = requireAllSides;
	}
}

public static class ImageUtils
{
	[Flags]
	public enum Side
	{
		Top = 1 << 0,
		Left = 1 << 1,
		Right = 1 << 2,
		Bottom = 1 << 3,

		All = 0x0F
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="sourceFile"></param>
	/// <returns></returns>
	public static bool AutoCropJpgSolid(string sourceFile, string outFile, uint color, float percentage = 1f)
	{
		RGBQUAD rgbColor = new RGBQUAD();
		rgbColor.uintValue = color;
		return AutoCropJpg(sourceFile, outFile, rgbColor, percentage, 0, new CropParams(Side.All, false));
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
		FreeImage.Unload(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		FreeImage.JPEGCrop(sourceFile, outFile, 0, 0, width - 1, height - amt - 1);

		FreeImage.Unload(bitmap);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns>True if the image was cropped.</returns>
	public static bool CropUwashWatermark(string sourceFile, string outFile)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, sourceFile, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP bitmap = FreeImage.ConvertTo32Bits(raw);
		FreeImage.Unload(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		int bottomBorder = GetBottomBorder(bitmap, 0xffffffff, 0.981f, 15);

		FreeImage.Unload(bitmap);

		if (bottomBorder >= 4 && bottomBorder <= 9)
		{
			return FreeImage.JPEGCrop(sourceFile, outFile, 0, 0, width, height - 30);
		}
		else
		{
			if (File.Exists(outFile)) File.Delete(outFile);
			File.Copy(sourceFile, outFile);
			return false;
		}
	}

	public static bool AutoCropJpg(string sourceFile, string outFile, RGBQUAD target, float percentage, int leeway)
	{
		return AutoCropJpg(sourceFile, outFile, target, percentage, leeway, new CropParams(Side.Left | Side.Right | Side.Top | Side.Bottom, false));
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="sourceFile"></param>
	/// <param name="outFile"></param>
	/// <param name="percentage">The ratio of a row or column that must be solid white for crop to detect</param>
	/// <param name="leeway">Difference allowed in each color channel to still detect as a match against target.</param>
	/// <param name="sides"></param>
	/// <param name="requireAllSides">If true, all the specified sides must be cropped.</param>
	public static bool AutoCropJpg(string sourceFile, string outFile, RGBQUAD target, float percentage, int leeway, CropParams cropParams)
	{
		FIBITMAP raw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_JPEG, sourceFile, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP bitmap = FreeImage.ConvertTo32Bits(raw);
		FreeImage.Unload(raw);

		int width = (int)FreeImage.GetWidth(bitmap);
		int height = (int)FreeImage.GetHeight(bitmap);

		int left = 0;
		int right = width;
		int bottom = 0;
		int top = height;

		// set to true if one of the 'sides' specified wasn't cropped
		bool hasUncroppedSide = false;
		
		// determine top
		if ((cropParams.Sides & Side.Top) != 0)
		{
			top = GetTopBorder(bitmap, target, percentage, leeway);
			hasUncroppedSide |= top == height;
		}

		// determine bottom
		if ((cropParams.Sides & Side.Bottom) != 0)
		{
			bottom = GetBottomBorder(bitmap, target, percentage, leeway);
			hasUncroppedSide |= bottom == 0;
		}

		// determine right
		if ((cropParams.Sides & Side.Right) != 0)
		{
			right = GetRightBorder(bitmap, target, percentage, leeway);
			hasUncroppedSide |= right == width;
		}

		// determine left
		if ((cropParams.Sides & Side.Left) != 0)
		{
			left = GetLeftBorder(bitmap, target, percentage, leeway);
			hasUncroppedSide |= left == 0;
		}

		FreeImage.Unload(bitmap);

		int newWidth = Math.Abs(right - left);
		int newHeight = Math.Abs(top - bottom);

		if (hasUncroppedSide && cropParams.RequireAllSides)
		{
			// at least one of the requested sides was not cropped
			return false;
		}

		if (left == 0 && right == width && bottom == 0 && top == height)
		{
			// that would be the whole image
			return false;
		}
		else if (Math.Abs(newWidth - width) < 6 && Math.Abs(newHeight - height) < 6)
		{
			// that's not very consequential
			return false;
		}
		else
		{
			return FreeImage.JPEGCrop(sourceFile, outFile, left, height - top, right, height - bottom);
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
