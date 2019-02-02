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
		public string[] additionalCategories;
		public string filenameSuffix;
		public string digitalCollectionsKey;
		public string sourceTemplate;
		public int minIndex;
		public int maxIndex;

		public bool allowCrop = true;
		public bool allowFailedCreators = false;

		public bool allowUpload = true;
		public bool allowDataDownload = true;
		public bool allowImageDownload = true;
	}
}
