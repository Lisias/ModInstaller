using System;
using System.IO;
using System.Reflection;

namespace ModInstaller.FormsUI
{
	internal static class EmbeddedAssemblies
	{
		internal static void Init()
		{
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name;
			if (dllName.EndsWith(".resources")) return null;

			Type t = typeof(EmbeddedAssemblies);

			dllName = string.Format("{0}.{1}.{2}.dll", t.Namespace, t.Name, dllName);
			Console.WriteLine(string.Format("Looking for {0}", dllName));

			try
			{
				byte[] bytes = null;
				using (Stream stream = t.Assembly.GetManifestResourceStream(dllName))
				{
					if (null == stream)
					{
						Console.WriteLine(string.Format("{0} NOT found!", dllName));
						return null;
					}
					using (MemoryStream ms = new MemoryStream())
					{
						stream.CopyTo(ms);
						bytes = ms.ToArray();
					}
				}
				Assembly r =  Assembly.Load(bytes);
				Console.WriteLine(string.Format("Found {0}", r.FullName));
				return r;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString() + " : " + ex.StackTrace);
				return null;
			}
		}
	}
}
