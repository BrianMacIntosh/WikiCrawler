﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiCrawler
{
	public struct Credentials
	{
		public string Username;
		public string Password;

		public Credentials(string username, string password)
		{
			Username = username;
			Password = password;
		}
	}
}
