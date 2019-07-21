using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaWiki
{
	public enum Namespace
	{
		Invalid = -99,
		Media = -2,
		Special = -1,
		Main = 0,
		Talk = 1,
		User = 2,
		User_talk = 3,
		Project = 4,
		Project_talk = 5,
		File = 6,
		File_talk = 7,
		MediaWiki = 8,
		MediaWiki_talk = 9,
		Template = 10,
		Template_talk = 11,
		Help = 12,
		Help_talk = 13,
		Category = 14,
		Category_talk = 15,
		Expression = 16,
		Expression_talk = 17,
		DefinedMeaning = 24,
		DefinedMeaning_talk = 25,
		Thread = 90,
		Thread_talk = 91,
		Summary = 92,
		Summary_talk = 93,
		COMMONS_Creator = 100,
		Item = 120,
		Item_talk = 121,
		Property = 122,
		Property_talk = 123,
	}
}
