using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WikiCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
			try
			{
				Console.Write("Task>");
				string task = Console.ReadLine();

				switch (task)
				{
					case "nsrw":
						NsrwFollowup.Do();
						break;
					case "uwash_cleancache":
						UWash.CacheCleanup();
						break;
					case "uwash_cleancreators":
						UWash.CreatorCleanup();
						break;
					case "uwash_rebuildfailures":
						UWash.RebuildFailures();
						break;
					case "uwash":
						UWash.Harvest();
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
