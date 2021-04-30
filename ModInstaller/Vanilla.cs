using System.Xml.Linq;

namespace ModInstaller
{
	internal class Vanilla
	{
		private readonly string folder;

		internal Vanilla(string folder, XElement xElement, string os)
		{
			this.folder = folder;
			xElement = xElement?.Element("File");
			this.Filename = xElement?.Element("Name")?.Value;
			this.Sha1 = xElement?.Element("SHA1").Element(os)?.Value;
		}
		internal string Filename { get; private set; }
		internal string Sha1 { get; private set; }
		internal bool IsEnabled => !Util.SHA1Equals(this.folder + "/Assembly-CSharp.dll", Sha1);
	}
}
