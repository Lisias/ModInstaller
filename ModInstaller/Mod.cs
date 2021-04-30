using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace ModInstaller
{
	public struct Mod
	{
		internal Mod(XElement mod) : this()
		{
			Name = mod.Element("Name")?.Value;
			Link = mod.Element("Link")?.Value;
			Files = mod.Element("Files")?.Elements("File")
						?.ToDictionary
						(
							element => element.Element("Name")?.Value,
							element => element.Element("SHA1")?.Value
						);
			Dependencies = mod.Element("Dependencies")
						?.Elements("string")
						.Select(dependency => dependency.Value)
						.ToList();
			Optional = mod.Element("Optional")
						?.Elements("string")
						.Select(dependency => dependency.Value)
						.ToList()
						?? new List<string>();
		}

		internal Mod(string filename, Dictionary<string, string> files) : this()
		{
			this.Name = filename;
			this.Files = files;
			Link = "";
			Dependencies = new List<string>();
			Optional = new List<string>();
		}

		public string Name { get; private set; }
		public Dictionary<string, string> Files { get; private set; }
		public string Link { get; private set; }
		public List<string> Dependencies { get; private set; }
		public List<string> Optional { get; private set; }
	}
}
