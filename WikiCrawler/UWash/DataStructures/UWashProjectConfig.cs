using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWash
{
	public class UWashProjectConfig
	{
		public string informationTemplate = "Photograph";
		public string defaultAuthor;
		public string defaultPubCountry;
		public string masterCategory;
		public string checkCategory;
		public string filenameSuffix;
		public string digitalCollectionsKey;
		public string sourceTemplate;
		public int minIndex;
		public int maxIndex;

		public bool allowUpload = true;
		public bool allowDataDownload = true;
		public bool allowImageDownload = true;
	}
}
