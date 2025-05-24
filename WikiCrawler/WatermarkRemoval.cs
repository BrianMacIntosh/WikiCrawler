using FreeImageAPI;
using ImageProcessing;
using MediaWiki;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using WikiCrawler;

public class WatermarkRemoval
{
	/// <summary>
	/// The category to act on.
	/// </summary>
	private PageTitle m_commonsCategory;

	private static WebClient s_client = new WebClient();

	public WatermarkRemoval(PageTitle commonsCategory)
	{
		m_commonsCategory = commonsCategory;
	}

	public void Download()
	{
		//Wikimedia.Article aarticle = GlobalAPIs.Commons.GetPage("File:\"Bokau\" ali \"šmon\" (z napisom W Bacus in okraski) za vino, Šlomberk 1953.jpg", "imageinfo", "iilimit=50&iiprop=comment%7Curl");

		string cacheDirectory = Path.Combine(Configuration.DataDirectory, "watermark", "originals");
		if (!Directory.Exists(cacheDirectory))
		{
			Directory.CreateDirectory(cacheDirectory);
		}
		foreach (Article article in GlobalAPIs.Commons.GetCategoryEntries(m_commonsCategory, cmtype: CMType.file))
		{
			string safeTitle = article.title.Name;
			safeTitle = string.Join("_", safeTitle.Split(Path.GetInvalidFileNameChars()));
			safeTitle = string.Join("_", safeTitle.Split(Path.GetInvalidPathChars()));

			if (Path.GetFileNameWithoutExtension(safeTitle).EndsWith(" (cropped)"))
			{
				continue;
			}
			
			string targetPath = Path.Combine(cacheDirectory, safeTitle);
			if (!File.Exists(targetPath))
			{
				Article imageInfo = GlobalAPIs.Commons.GetPage(article, prop: Prop.imageinfo, iilimit: 50, iiprop: Api.BuildParameterList(IIProp.comment, IIProp.url));

				// download the original revision
				string url = imageInfo.imageinfo.Last().url;

				Console.WriteLine(article.title);
				GlobalAPIs.Commons.EditThrottle.WaitForDelay();
				s_client.DownloadFile(url, targetPath);
			}
		}
	}

	public void FindWatermark()
	{
		FIBITMAP watermarkRaw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_BMP, "E:/Wikidata/watermark/watermark01-nojpg.bmp", FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP watermarkBitmap = FreeImage.ConvertTo32Bits(watermarkRaw);

		int width = (int)FreeImage.GetWidth(watermarkBitmap);
		int height = (int)FreeImage.GetHeight(watermarkBitmap);

		float f1 = 0.81960784313725490196078431372549f;
		float c1 = 0.92549019607843137254901960784314f;
		float f2 = 0.44705882352941176470588235294118f;
		float c2 = 0.0f;
		float a = 0.59745762711864406779661016949153f;

		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < width; y++)
			{
				RGBQUAD color;
				FreeImage.GetPixelColor(watermarkBitmap, (uint)x, (uint)y, out color);
				FloatColor floatColor = new FloatColor(color);
				floatColor.A = color.rgbBlue > 0 ? a : 0;
				floatColor.R = floatColor.R / a;
				floatColor.G = floatColor.G / a;
				floatColor.B = floatColor.B / a;
				color = (RGBQUAD)floatColor;
				FreeImage.SetPixelColor(watermarkBitmap, (uint)x, (uint)y, ref color);
			}
		}

		FreeImage.Save(FREE_IMAGE_FORMAT.FIF_PNG, watermarkBitmap, "E:/Wikidata/watermark/watermark_out.png", FREE_IMAGE_SAVE_FLAGS.DEFAULT);
	}
	
	public void RemoveAndUpload()
	{
		SetOutputMessage(OutputMessage);

		FIBITMAP watermarkRaw = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_PNG, "E:/Wikidata/watermark/watermark_out.png", FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		FIBITMAP watermarkBitmap = FreeImage.ConvertTo32Bits(watermarkRaw);
		uint wmWidth = FreeImage.GetWidth(watermarkBitmap);
		uint wmHeight = FreeImage.GetHeight(watermarkBitmap);

		//TODO: check for separate (cropped) files
		//TODO: remove {{watermark}}

		//foreach (string file in Directory.GetFiles(Path.Combine(Configuration.DataDirectory, "watermark", "originals")))
		string file = "E:/WikiData/watermark/Originals/Hram pri Gregorjevih, Podgrič 1958.jpg";
		{
			Console.WriteLine(file);

			FIBITMAP image = FreeImage.LoadEx(file, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
			image = FreeImage.ConvertTo32Bits(image);

			//TODO: use convolution to locate the watermark
			uint wmX = FreeImage.GetWidth(image) - 256;
			uint wmY = 9;

			// remove watermark
			RGBQUAD wmColor;
			FloatColor wmFloat;
			RGBQUAD imageColor;
			FloatColor imageFloat;
			for (uint rx = 0; rx < wmWidth; rx++)
			{
				for (uint ry = 0; ry < wmHeight; ry++)
				{
					if (!FreeImage.GetPixelColor(watermarkBitmap, rx, ry, out wmColor))
					{
						throw new Exception();
					}
					if (!FreeImage.GetPixelColor(image, rx + wmX, ry + wmY, out imageColor))
					{
						throw new Exception();
					}

					if (wmColor.rgbReserved > 0)
					{
						imageFloat = new FloatColor(imageColor);
						wmFloat = new FloatColor(wmColor);
						imageFloat.R = (imageFloat.R - wmFloat.A * wmFloat.R) / (1 - wmFloat.A);
						imageFloat.G = (imageFloat.G - wmFloat.A * wmFloat.G) / (1 - wmFloat.A);
						imageFloat.B = (imageFloat.B - wmFloat.A * wmFloat.B) / (1 - wmFloat.A);
						imageColor = (RGBQUAD)imageFloat;
						FreeImage.SetPixelColor(image, rx + wmX, ry + wmY, ref imageColor);
					}
				}
			}

			string filename = Path.Combine(Configuration.DataDirectory, "watermark", "Results", Path.GetFileName(file));
			FreeImage.SaveEx(image, filename, FREE_IMAGE_SAVE_FLAGS.JPEG_QUALITYGOOD);
		}
	}

	static void OutputMessage(FREE_IMAGE_FORMAT format, string message)
	{
		throw new Exception(message);
	}

	[DllImport("FreeImage", EntryPoint = "FreeImage_SetOutputMessage")]
	internal static extern void SetOutputMessage(OutputMessageFunction outputMessageFunction);
}
