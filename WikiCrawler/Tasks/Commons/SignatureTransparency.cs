using FreeImageAPI;
using MediaWiki;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;

namespace Tasks.Commons
{
	public class SignatureTransparency : BatchTask
	{
		private WebClient WebClient = new WebClient();

		public SignatureTransparency() : base("signatures")
		{
		}

		public override void Execute()
		{
			string targetsFile = Path.Combine(ProjectDataDirectory, "targets.txt");
			List<string> targets = File.ReadAllLines(targetsFile).ToList();
			for (int i = targets.Count - 1; i >= 0; i--)
			{
				PageTitle pageTitle = PageTitle.Parse(targets[i]);
				if (string.IsNullOrEmpty(pageTitle.Namespace))
				{
					pageTitle.Namespace = PageTitle.NS_File;
				}
				try
				{
					ProcessFile(pageTitle);
					targets.RemoveAt(i);
				}
				catch (Exception e)
				{
					ConsoleUtility.WriteLine(ConsoleColor.Red, e.ToString());
				}
			}
			File.WriteAllLines(targetsFile, targets);
		}

		private void ProcessFile(PageTitle file)
		{
			Article article = GlobalAPIs.Commons.GetPage(file, "info|revisions|imageinfo", "url");

			// download image
			string imagePath = CacheImage(article);
			string imageExtension = Path.GetExtension(imagePath);
			string newFile = Path.Combine(ProjectDataDirectory, "converted", Path.GetFileNameWithoutExtension(imagePath) + ".png");

			// load image
			using (FreeImageBitmap sourceImage = new FreeImageBitmap(imagePath))
			{
				using (Bitmap newImage = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb))
				{
					// greyscale histogram
					int pixelCount = sourceImage.Width * sourceImage.Height;
					byte[] histogram = new byte[256];
					for (int x = 0; x < sourceImage.Width; x++)
					{
						for (int y = 0; y < sourceImage.Height; y++)
						{
							Color pixel = sourceImage.GetPixel(x, y);
							float grey = (pixel.R + pixel.G + pixel.B) / 3f;
							histogram[(int)grey]++;
						}
					}

					// identify histogram limits
					int greyMin = 0;
					int greyMax = 240;
					int blackTruncateCount = (int)(0.0025f * pixelCount);
					for (int accumulator = 0; greyMin < histogram.Length && accumulator < blackTruncateCount; accumulator += histogram[greyMin], greyMin++) ;
					//TODO: automatically identify "white hump" at top end of histogram and truncate

					// perform the transparency conversion
					for (int x = 0; x < sourceImage.Width; x++)
					{
						for (int y = 0; y < sourceImage.Height; y++)
						{
							Color pixel = sourceImage.GetPixel(x, y);
							float grey = (pixel.R + pixel.G + pixel.B) / 3f;

							grey = (grey - greyMin) / (greyMax - greyMin);
							grey = Math.Min(Math.Max(0, grey), 1) * 255;

							Color newPixel = Color.FromArgb(255 - (int)grey, 0, 0, 0);
							newImage.SetPixel(x, newImage.Height - y - 1, newPixel);
						}
					}

					// save the new image
					Directory.CreateDirectory(Path.GetDirectoryName(newFile));
					newImage.Save(newFile, ImageFormat.Png);
				}
			}

			// upload the new image
			if (m_config.allowUpload)
			{
				if (imageExtension.Equals(".png", StringComparison.OrdinalIgnoreCase) || imageExtension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
				{
					// overwrite
					GlobalAPIs.Commons.UploadFromLocal(article, newFile, "Making signature background transparent", bot: false);
				}
				else
				{
					// upload new file
					PageTitle newTitle = new PageTitle(PageTitle.NS_File, Path.GetFileNameWithoutExtension(file.Name) + " (transparent).png");
					Article newArticle = new Article(newTitle, article.revisions[0].text);
					CommonsFileWorksheet newFileWorksheet = new CommonsFileWorksheet(newArticle);
					newFileWorksheet.AddOtherVersion(file);

					// record as 'other version'
					CommonsFileWorksheet originalFilePage = new CommonsFileWorksheet(article);
					originalFilePage.AddOtherVersion(newTitle);

					GlobalAPIs.Commons.UploadFromLocal(newFileWorksheet.Article, newFile, "Creating transparent variant of signature", bot: false);
					GlobalAPIs.Commons.SetPage(originalFilePage.Article, "Linking transparent variant of signature", bot: false);

					// replace uses on other wikis
					//TODO:
				}

				File.Delete(newFile);
				File.Delete(imagePath);
			}
		}

		private string CacheImage(Article article)
		{
			string url = article.imageinfo[0].url;
			Uri uri = new Uri(url);
			string extension = Path.GetExtension(url);
			string imagepath = Path.Combine(ImageCacheDirectory, string.Join("", article.title.Name.Split(Path.GetInvalidFileNameChars())));
			if (!File.Exists(imagepath) && m_config.allowImageDownload)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(imagepath));

				Console.WriteLine("Downloading image: " + article.title);
				WebThrottle.Get(uri).WaitForDelay();
				WebClient.Headers.Add("user-agent", Api.UserAgent);
				WebClient.DownloadFile(uri, imagepath);
			}
			return imagepath;
		}
	}
}
