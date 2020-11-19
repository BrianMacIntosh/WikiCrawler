using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWash
{
	public class UWashProjectConfig : ProjectConfig
	{
		public string digitalCollectionsKey;
		public int minIndex;
		public int maxIndex;

		public string department = "{{UWASH-Special-Collections}}";

		public bool allowCrop = true;
	}
}
