using System;
using System.Xml.Linq;

namespace ModInstaller
{
	public class Installer
	{
		private readonly string folder;
		internal Installer(string folder, XElement xElement)
		{
			this.folder = folder;
			this.Link = xElement?.Element("Link").Value;
			this.Sha1 = xElement?.Element("SHA1").Value;
			this.AULink = xElement?.Element("AULink").Value;
		}
		internal string Link { get; private set; }
		internal string Sha1 { get; private set; }
		internal string AULink { get; private set; }
	}
}
