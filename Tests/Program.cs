using System;
using ModInstaller;

namespace Tests
{
	class Settings : ISettings
	{
		string _modFolder;
		string ISettings.modFolder { get => _modFolder; set => _modFolder = value; }

		string _APIFolder;
		string ISettings.APIFolder { get => _APIFolder; set => _APIFolder = value; }

		internal string _installFolder;
		string ISettings.installFolder { get => _installFolder; set => _installFolder = value; }

		string _temp;
		string ISettings.temp { get => _temp; set => _temp = value; }

		void ISettings.Save()
		{
			Console.WriteLine(string.Format(
					"\"Saving\" settings as follows:"
					+ "\n\tmodFolder : {0}"
					+ "\n\tAPIFolder : {1}"
					+ "\n\tinstallFolder : {2}"
					+ "\n\ttemp : {3}"
					, _modFolder
					, _APIFolder
					, _installFolder
					, _temp
				));
		}
	}

	class InstallationPathListener : Manager.InstallationPathListener
	{
		const string home = "/Users/lisias/Applications/Games/GOG/Hollow Knight.app";
		private readonly Settings settings;

		public InstallationPathListener(Settings settings)
		{
			this.settings = settings;
		}

		bool Manager.InstallationPathListener.IsInstallationPath(string path)
		{
			return home == path;
		}

		void Manager.InstallationPathListener.SetInstallationPathManually()
		{
			this.settings._installFolder = home;
		}
	}

	class InstallResultListener : Manager.InstallResultListener
	{
		DownloadProgressListener Manager.InstallResultListener.Download(Uri uri, string path, string name)
		{
			return new DownloadProgress(name); // Let's Manager do all the heavy lifting.
		}

		void Manager.InstallResultListener.InstalledWithSuccess(ModEntry entry)
		{
			Console.WriteLine(string.Format("{0} was *INSTALLED* with success.", entry.Name));
		}

		ModEntry Manager.InstallResultListener.Register(ModEntry entry)
		{
			Console.WriteLine(string.Format("{0} was registered {1} {2}", entry.Name, entry.IsInstalled, entry.IsEnabled));
			return entry;
		}

		void Manager.InstallResultListener.UninstalledWithSuccess(ModEntry entry)
		{
			Console.WriteLine(string.Format("{0} was *UNINSTALLED* with success.", entry.Name));
		}

		void Manager.InstallResultListener.UpdatedWithSuccess(ModEntry entry)
		{
			Console.WriteLine(string.Format("{0} was *UPDATED* with success.", entry.Name));
		}
	}

	class DownloadProgress : DownloadProgressListener
	{
		private readonly string name;

		public DownloadProgress(string name)
		{
			this.name = name;
		}

		void DownloadProgressListener.Aborted(Exception e)
		{
			Console.WriteLine(string.Format("Download for {0} was *ABORTED* due {1}!", this.name, e.Message));
		}

		void DownloadProgressListener.Cancelled()
		{
			Console.WriteLine(string.Format("Download for {0} was *CANCELED*!", this.name));
		}

		void DownloadProgressListener.Completed(long sizeInBytes, long timeInMilliseconds)
		{
			Console.WriteLine(string.Format("Download for {0} was *COMPLETED* in {1} secs with {2} Kb.", this.name, timeInMilliseconds/1000, sizeInBytes/1024));
		}

		void DownloadProgressListener.Progress(long bytesReceived, long totalBytesToReceive, long elapsedMilliseconds)
		{
			Console.WriteLine(string.Format("Downloading {0} : {1:0.00}% at {2:0.000}", this.name, bytesReceived/totalBytesToReceive, elapsedMilliseconds/1000));
		}
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			Settings settings = new Settings();
			InstallationPathListener pathListener = new InstallationPathListener(settings);
			InstallResultListener installListener = new InstallResultListener();
			Manager m = new Manager(settings, pathListener);

			//m.CheckUpdate();
			//m.PiracyCheck();
			if (m.CheckApiInstalled(installListener))
				Console.WriteLine("Modding API successfully installed!");
			m.SearchInstalledFiles(installListener);

			Console.WriteLine("Available Mods:");
			foreach (Mod e in m.ModsSortedList)
				Console.WriteLine(string.Format("\tFound {0} from {1}.", e.Name, e.Link));

			Console.WriteLine("Installed Mods:");
			foreach (String e in m.InstalledMods)
				Console.WriteLine(string.Format("\t{0}", e));
		}
	}
}
