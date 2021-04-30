using System;

namespace ModInstaller.FormsUI
{
	public static class Manager
	{
		private static ModInstaller.Manager instance;
		internal static ModInstaller.Manager Instance => instance;

		internal static void Initialise(ModInstaller.Manager.InstallationPathListener listener)
		{
			instance = new ModInstaller.Manager(new Storage(), listener); 
		}

		internal static string OS => ModInstaller.Manager.OS;
	}

	public class Storage : ModInstaller.ISettings
	{
		string ISettings.modFolder { get => Properties.Settings.Default.modFolder; set => Properties.Settings.Default.modFolder = value; }
		string ISettings.APIFolder { get => Properties.Settings.Default.APIFolder; set => Properties.Settings.Default.APIFolder = value; }
		string ISettings.installFolder { get => Properties.Settings.Default.installFolder; set => Properties.Settings.Default.installFolder = value; }
		string ISettings.temp { get => Properties.Settings.Default.temp; set => Properties.Settings.Default.temp = value; }

		void ISettings.Save()
		{
			Properties.Settings.Default.Save();
		}
	}
}
