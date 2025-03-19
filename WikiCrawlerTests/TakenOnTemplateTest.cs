using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

[TestClass()]
public class TakenOnTemplateTest
{
	private class ToISOTest
	{
		public string input;
		public string date;
		public bool hasDay;
	}

	[TestMethod()]
	public void DateToISOTest()
	{
		List<ToISOTest> testCases = new List<ToISOTest>()
			{
				new ToISOTest() { input = "June 3rd, 1992", date = "1992-06-03", hasDay = true },
				new ToISOTest() { input = "June 10th, 1992", date = "1992-06-10", hasDay = true },
				new ToISOTest() { input = "2014", date = "2014", hasDay = false },
				new ToISOTest() { input = "2014-03", date = "2014-03", hasDay = false },
				new ToISOTest() { input = "2014-03-12", date = "2014-03-12", hasDay = true },
				new ToISOTest() { input = "2014/03/12", date = "2014-03-12", hasDay = true },
				new ToISOTest() { input = "2014.03.12", date = "2014-03-12", hasDay = true },
				new ToISOTest() { input = "2014 03 12", date = "2014-03-12", hasDay = true },
				new ToISOTest() { input = "2014\\03\\12", date = "2014-03-12", hasDay = true },
				new ToISOTest() { input = "03-01-2", date = "", hasDay = false },
				new ToISOTest() { input = "03-01-02", date = "", hasDay = false },
				new ToISOTest() { input = "03-01-22", date = "", hasDay = false },
				new ToISOTest() { input = "", date = "", hasDay = false },
				new ToISOTest() { input = "01-1992-03", date = "", hasDay = false },
			};
		foreach (ToISOTest test in testCases)
		{
			bool actualHasDay;
			string actual = WikiCrawler.TakenOnTemplate.DateToISO(test.input, out actualHasDay);
			Assert.AreEqual(test.hasDay, actualHasDay);
			Assert.AreEqual(test.date, actual);
		}
	}

	[TestMethod()]
	public void ReplaceDateTest()
	{
		/*string date = string.Empty; // TODO: Initialize to an appropriate value
		string metadate = string.Empty; // TODO: Initialize to an appropriate value
		string newcontent = string.Empty; // TODO: Initialize to an appropriate value
		string newcontentExpected = string.Empty; // TODO: Initialize to an appropriate value
		string yyyymmdd = string.Empty; // TODO: Initialize to an appropriate value
		string yyyymmddExpected = string.Empty; // TODO: Initialize to an appropriate value
		bool expected = false; // TODO: Initialize to an appropriate value
		bool actual;
		actual = TakenOnTemplate.ReplaceDate(date, metadate, out newcontent, out yyyymmdd);
		Assert.AreEqual(newcontentExpected, newcontent);
		Assert.AreEqual(yyyymmddExpected, yyyymmdd);
		Assert.AreEqual(expected, actual);*/
	}
}
