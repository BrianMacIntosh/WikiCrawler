using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UWash
{
	public struct UWashFailure
	{
		public int Index;
		public string Reason;

		public UWashFailure(int index, string reason)
		{
			Index = index;
			Reason = reason;
		}
	}
}
