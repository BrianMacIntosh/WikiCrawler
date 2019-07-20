using System;
using System.Diagnostics;
using System.Net;

namespace WikiCrawler
{
	class Program
    {
        static void Main(string[] args)
        {
			//string croppath = "uwash_images/53_cropped.jpg";
			//ImageUtils.AutoCropJpg("uwash_images/53.jpg", croppath, 0xffffffff, 0.97f, 5);

			ServicePointManager.Expect100Continue = true;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
				   | SecurityProtocolType.Tls11
				   | SecurityProtocolType.Tls12
				   | SecurityProtocolType.Ssl3;

			try
			{
				Console.Write("Task>");
				string task = Console.ReadLine();

				switch (task)
				{
					case "watermarkdl":
						new WatermarkRemoval("Category:Images from Slovenian Ethnographic Museum website").Download();
						break;
					case "watermarkfind":
						new WatermarkRemoval("Category:Images from Slovenian Ethnographic Museum website").FindWatermark();
						break;
					case "watermarkremove":
						new WatermarkRemoval("Category:Images from Slovenian Ethnographic Museum website").RemoveAndUpload();
						break;
					case "npassets":
						{
							NPGallery.NPGalleryAssetListDownloader albumDl = new NPGallery.NPGalleryAssetListDownloader("npgallery");
							albumDl.DownloadAll();
						}
						break;
					case "dropbox":
						DropboxDownloader.Do();
						break;
					case "catmove":
						RequestMassCatMove.Do();
						break;
					case "nsrw":
						NsrwFollowup.Do();
						break;
					case "batchrebuild":
						BatchController.RebuildSuccesses();
						break;
					case "batchrevalidate":
						BatchController.RevalidateDownloads();
						break;
					case "batchdownall":
						BatchController.DownloadAll();
						break;
					case "batchdown":
						BatchController.Download();
						break;
					case "batchup":
						BatchController.Upload();
						break;
					case "taxoncat":
						TaxonCategoryUpdate.Do();
						break;
					case "creators":
						WikidataCreatorPropagation.Do();
						break;
					case "newcreators":
						CommonsCreatorFromWikidata.MakeCreators();
						break;
					case "autocreators":
						//needs rerun up to 'Cavalcanti'
						CommonsCreatorFromWikidata.MakeCreatorsFromCat();
						break;
					case "implicitcreators":
						FixImplicitCreators.Do();
						break;
					case "pdold":
						PdOldAuto.Do();
						break;
					case "massdownload":
						MassDownloader.Do();
						break;
					case "nulleditlinks":
						LinksNullEditor.Do();
						break;
					case "massrevert":
						LinksNullEditor.Revert();
						break;
					case "findcatcreators":
						FindCategoryCreators.Find();
						break;
					case "addcheck":
						LinksNullEditor.CheckCat();
						break;
					default:
						Console.WriteLine("No such task.");
						break;
				}
				//UWashCats.Do();
				//SingleUpload.Do();
				//NsrwFollowup.Do();
			}
			/*catch (Exception e)
			{
				Console.WriteLine(e);
				using (StreamWriter writer = new StreamWriter(new FileStream("errorlog.txt", FileMode.Create)))
				{
					writer.WriteLine(e.ToString());
				}
			}*/
			finally
			{
				//TODO: check dirty
				CategoryTranslation.SaveOut();
			}

			Console.WriteLine("Done");
			WindowsUtility.FlashWindowEx(Process.GetCurrentProcess().MainWindowHandle);
			Console.ReadLine();
        }
    }
}
