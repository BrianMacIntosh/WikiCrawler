using System;
using System.IO;

namespace WikiCrawler
{
	class Program
    {
        static void Main(string[] args)
        {
			//string croppath = "uwash_images/53_cropped.jpg";
			//ImageUtils.AutoCropJpg("uwash_images/53.jpg", croppath, 0xffffffff, 0.97f, 5);

			try
			{
				Console.Write("Task>");
				string task = Console.ReadLine();

				switch (task)
				{
					case "dropbox":
						DropboxDownloader.Do();
						break;
					case "catmove":
						RequestMassCatMove.Do();
						break;
					case "nsrw":
						NsrwFollowup.Do();
						break;
					case "uwash_cleancache":
						UWash.UWashController.CacheCleanup();
						break;
					case "uwash_rebuildfailures":
						UWash.UWashController.RebuildFailures();
						break;
					case "uwash":
						UWash.UWashController.Harvest();
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
					default:
						Console.WriteLine("No such task.");
						break;
				}
				//UWashCats.Do();
				//SingleUpload.Do();
				//NsrwFollowup.Do();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				using (StreamWriter writer = new StreamWriter(new FileStream("errorlog.txt", FileMode.Create)))
				{
					writer.WriteLine(e.ToString());
				}
			}

			Console.WriteLine("Done");
			Console.ReadLine();
        }
    }
}
