using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWash
{
	public class UWashConfig
	{
		public bool mapCategories = true;
		public bool remapCategories = false;
		public bool createCreators = false;
		public int maxFailed = int.MaxValue;
		public int maxNew = int.MaxValue;
		public int maxSuccesses = int.MaxValue;
		public int saveOutInterval = 5;
	}
}
