using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// JSON deserialization target for a project configuration file.
/// </summary>
public class ProjectConfig
{
	public string informationTemplate = "Photograph";
	public string defaultAuthor;
	public string defaultPubCountry;
	public string masterCategory;
	public string checkCategory;
	public string[] additionalCategories;
	public string filenameSuffix;

	public bool allowFailedCreators = false;

	public bool allowUpload = true;
	public bool allowDataDownload = true;
	public bool allowImageDownload = true;

	public int maxFailed = int.MaxValue;
	public int maxNew = int.MaxValue;
	public int maxSuccesses = int.MaxValue;
}
