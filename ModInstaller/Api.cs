using System.Xml.Linq;

namespace ModInstaller
{
	internal class Api
	{
		private readonly string folder;
		internal Api(string folder, XElement xElement, string os)
		{
			this.folder = folder;
			this.Name = xElement?.Element("Name").Value;
			this.fillFile(xElement?.Element("Files"), os);
			this.Link = this.fillLink(xElement?.Element("Link"), os);
		}
		private void fillFile(XElement xElement, string os)
		{
			xElement = xElement?.Element("File");
			this.Filename = xElement?.Element("Name")?.Value;
			this.Sha1 = xElement?.Element("SHA1").Element(os)?.Value;
			this.Patch = xElement?.Element("Patch")?.Value;
		}
		private string fillLink(XElement xElement, string os)
		{
			return xElement?.Element(os)?.Value;
		}

		internal string Name { get; private set; }
		internal string Filename { get; private set; }
		internal string Sha1 { get; private set; }
		internal string Patch { get; private set; }
		internal string Link { get; private set; }
	}
}
