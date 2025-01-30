using MediaWiki;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public abstract class MappingValue
{
	/// <summary>
	/// Pages the value appears on.
	/// </summary>
	public List<string> FromPages = new List<string>();
}

public class MappingDate : MappingValue
{
	/// <summary>
	/// A date to replace the source with.
	/// </summary>
	public string ReplaceDate;

	/// <summary>
	/// The latest possible year this string could refer to.
	/// </summary>
	public int LatestYear = 9999;
}

/// <summary>
/// Stores data that needs to be manually mapped to a value.
/// </summary>
public class ManualMapping<MappingType> where MappingType : MappingValue, new()
{
	private Dictionary<string, MappingType> m_mappings = new Dictionary<string, MappingType>();

	private bool m_isDirty = false;

	public readonly string Filename;

	public ManualMapping(string filename)
	{
		Filename = filename;
		Deserialize();
	}

	public void Serialize()
	{
		if (m_isDirty)
		{
			ForceSerialize();
		}
	}

	public void ForceSerialize()
	{
		// write data
		using (StreamWriter writer = new StreamWriter(new FileStream(Filename, FileMode.Create, FileAccess.Write), Encoding.UTF8))
		{
			writer.WriteLine("{");
			List<KeyValuePair<string, MappingType>> mappingsFlat = new List<KeyValuePair<string, MappingType>>(m_mappings);
			bool first = true;
			foreach (KeyValuePair<string, MappingType> kv in mappingsFlat
				.OrderByDescending(kv => kv.Value.FromPages.Count))
			{
				if (!first)
				{
					writer.WriteLine(',');
				}
				else
				{
					first = false;
				}
				writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Key));
				writer.Write(":");
				writer.Write(Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value, Newtonsoft.Json.Formatting.Indented));
			}
			writer.WriteLine("\n}");
		}

		m_isDirty = false;
	}

	public void Deserialize()
	{
		if (File.Exists(Filename))
		{
			m_mappings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, MappingType>>(File.ReadAllText(Filename, Encoding.UTF8));
			m_isDirty = false;
		}
	}

	public MappingType TryMapValue(string input, PageTitle onPage)
	{
		MappingType mapping;
		if (!m_mappings.TryGetValue(input, out mapping))
		{
			mapping = new MappingType();
			m_mappings.Add(input, mapping);
		}
		if (mapping.FromPages.AddUnique(onPage.ToString()))
		{
			m_isDirty = true;
		}
		return mapping;
	}
}
