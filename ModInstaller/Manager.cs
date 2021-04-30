using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Security;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace ModInstaller
{
	public class Manager
	{
		public interface InstallationPathListener
		{
			bool IsInstallationPath(string path);
			void SetInstallationPathManually();
		}

		public interface InstallResultListener
		{
			ModEntry Register(ModEntry entry);

			// Should return true if the caller did the download itself, or false if it didn't.
			bool Download(Uri uri, string path, string name);

			void InstalledWithSuccess(ModEntry entry);
			void UninstalledWithSuccess(ModEntry entry);
			void UpdatedWithSuccess(ModEntry entry);
		}

		//private const string WebStore = "https://store.steampowered.com/app/367520/Hollow_Knight/";
		private const string WebStore = "https://www.gog.com/game/hollow_knight/"; // I prefer GOG! :)

		//private const string ModLinks = "https://raw.githubusercontent.com/Ayugradow/ModInstaller/master/modlinks.xml";
		//private const string ModLinks = "https://raw.githubusercontent.com/net-lisias-hk/ModInstaller/master/modlinks.xml";
		private const string ModLinks = "file:///Users/lisias/Workspaces/HollowKnight/ModInstaller/modlinks.xml";

		public const string Version = "v8.7.2 /L";
		public const string Author = "Lisias";

		private Installer installer;
		private Vanilla vanilla;
		public bool VanillaEnabled => this.vanilla.IsEnabled;

		private Api api;
		public bool ApiIsInstalled { get; private set; }
		public bool AssemblyIsAPI { get; private set; }
		public string CurrentPatch => this.api.Patch;

		private static string _OS;
		public static string OS => _OS ?? (_OS = Util.GetCurrentOS());

		public string OSPath => 
			OS == "MacOS"
				? $"{this.settings.installFolder}/Contents/Resources/Data/Managed"
				: $"{this.settings.installFolder}/hollow_knight_Data/Managed";

		private readonly List<string> defaultPaths = new List<string>();
		public List<string> DefaultPaths => defaultPaths;

		private readonly List<string> allMods = new List<string>();
		public List<string> AllMods => allMods;
 
		private readonly List<string> installedMods = new List<string>();
		public List<string>InstalledMods=> installedMods;

		private readonly List<Mod> modsList = new List<Mod>();
		public List<Mod> ModsList => modsList;
		public List<Mod> ModsSortedList => modsList.OrderBy(mod => mod.Name).ToList();

		private readonly List<ModEntry> modEntries = new List<ModEntry>();
		private readonly ISettings settings;

		public List<ModEntry> ModEntries => modEntries;

		public Manager(ISettings settings, InstallationPathListener pathListener)
		{
			this.settings = settings;
			this.FillDefaultPaths();
			this.CheckLocalInstallation(pathListener);
			this.LoadModLinks();
		}

		#region Initialization and Self Update.

		public void CheckUpdate()
		{
			string dir = AppDomain.CurrentDomain.BaseDirectory;
			string file = Path.GetFileName(Assembly.GetEntryAssembly()?.Location);

			if (File.Exists($"{dir}/lol.exe"))
				File.Delete($"{dir}/lol.exe");

			if (File.Exists($"{dir}/AU.exe"))
				File.Delete($"{dir}/AU.exe");

			// If the SHA1s are non-equal, update.
			if (null == this.installer || Util.SHA1Equals($"{dir}/{file}", installer.Sha1)) return;

			Util.DownloadFile(this.installer, dir);
			Util.Execute(this.installer, dir, file);
		}

		public void SetInstallationPath(string selectedPath)
		{
			this.settings.installFolder = selectedPath;
			this.settings.APIFolder = this.OSPath;
			this.settings.modFolder = $"{this.settings.APIFolder}/Mods";
			this.settings.Save();
			if (!Directory.Exists(this.settings.modFolder))
				Directory.CreateDirectory(this.settings.modFolder);
		}

		private void FillDefaultPaths()
		{
			switch (OS)
			{
				case "Windows":
					//Default Steam and GOG install paths for Windows.
					this.defaultPaths.Add("Program Files (x86)/Steam/steamapps/Common/Hollow Knight");
					this.defaultPaths.Add("Program Files/Steam/steamapps/Common/Hollow Knight");
					this.defaultPaths.Add("Steam/steamapps/common/Hollow Knight");
					this.defaultPaths.Add("Program Files (x86)/GOG Galaxy/Games/Hollow Knight");
					this.defaultPaths.Add("Program Files/GOG Galaxy/Games/Hollow Knight");
					this.defaultPaths.Add("GOG Galaxy/Games/Hollow Knight");
					break;
				case "Linux":
					// Default steam installation path for Linux.
					this.defaultPaths.Add(Environment.GetEnvironmentVariable("HOME") + "/.steam/steam/steamapps/common/Hollow Knight");
					break;
				case "MacOS":
					//Default steam installation path for Mac.
					this.defaultPaths.Add(Environment.GetEnvironmentVariable("HOME") + "/Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app");
					break;
			}
		}

		private void CheckLocalInstallation(InstallationPathListener installationPathCallback)
		{
			string installFolder = this.settings.installFolder;
			if (string.IsNullOrEmpty(installFolder))
			{
				DriveInfo[] allDrives = DriveInfo.GetDrives();

				foreach (DriveInfo d in allDrives.Where(d => d.DriveType == DriveType.Fixed))
				{
					foreach (string path in this.defaultPaths)
					{
						if (!Directory.Exists($"{d.Name}{path}")) continue;

						string p = $"{d.Name}{path}";
						if (installationPathCallback.IsInstallationPath(p))
						{
							installFolder = p;
							SetDefaultPath(p);
							CheckTemporary(d.Name);
						}

						if (!string.IsNullOrEmpty(installFolder))
							break;
					}
				}

				if (string.IsNullOrEmpty(installFolder))
				{
					installationPathCallback.SetInstallationPathManually();
					SetDefaultPath(this.settings.installFolder);
					CheckTemporary();
				}
			}
		}

		private void SetDefaultPath(string path)
		{
			this.settings.installFolder = path;
			this.settings.APIFolder = OSPath;
			this.settings.modFolder = $"{this.settings.APIFolder}/Mods";
			if (!Directory.Exists(this.settings.modFolder))
			{
				Directory.CreateDirectory(this.settings.modFolder);
			}
			this.settings.Save();
		}

		public void PiracyCheck()
		{
			if
			(
				OS != "Windows"
				|| File.Exists($"{this.settings.installFolder}/Galaxy.dll")
				|| Path.GetFileName(this.settings.installFolder) != "Hollow Knight Godmaster"
			)
				return;

			Util.Open(WebStore);
			throw new VerificationException("Please purchase the game before attempting to play it.");
		}

		public bool CheckApiInstalled(InstallResultListener callback)
		{
			if (!Directory.Exists(this.settings.APIFolder))
			{
				// Make sure to not ruin everything forever
				this.settings.installFolder = null;

				throw new VerificationException("Folder does not exist! (Game is probably not installed) Exiting.");
			}

			// Check if either API is installed or if vanilla dll still exists
			if (File.Exists($"{this.settings.APIFolder}/Assembly-CSharp.dll"))
			{
				byte[] bytes = File.ReadAllBytes($"{this.settings.APIFolder}/Assembly-CSharp.dll");
				Assembly asm = Assembly.Load(bytes);

				Type[] types;
				try
				{
					types = asm.GetTypes();
				}
				catch (ReflectionTypeLoadException e)
				{
					types = e.Types;
				}

				Type[] nonNullTypes = types.Where(t => t != null).ToArray();

				this.AssemblyIsAPI = nonNullTypes.Any(type => type.Name.Contains("CanvasUtil"));

				this.ApiIsInstalled = this.AssemblyIsAPI || File.Exists($"{this.settings.APIFolder}/Assembly-CSharp.mod");

				if (!File.Exists($"{this.settings.APIFolder}/Assembly-CSharp.vanilla")
					&& !this.ApiIsInstalled
					&& (!nonNullTypes.Any(type => type.Name.Contains("Constant"))
						|| (string)nonNullTypes
									.First(type => type.Name.Contains("Constant") && type.GetFields().Any(f => f.Name == "GAME_VERSION"))
									.GetField("GAME_VERSION")
									.GetValue(null)
						!= this.CurrentPatch))
				{
					// Make sure to not ruin everything forever part2
					throw new VerificationException("This installer requires the most recent stable version to run.\nPlease update your game to current stable patch and then try again.");
				}
			}
			else
			{
				// Make sure to not ruin everything forever part3
				throw new VerificationException("Unable to locate game files.\nPlease make sure the game is installed and then try again.");
			}

			if (!this.ApiIsInstalled || this.AssemblyIsAPI && !Util.SHA1Equals($"{this.settings.APIFolder}/Assembly-CSharp.dll", this.api.Sha1))
			{
				if (!callback.Download (
						new Uri(this.api.Link),
						$"{this.settings.installFolder}/Modding API.zip",
						"Modding API"
					))
					Util.DownloadFile(this.api, this.settings.installFolder);

				InstallApi
				(
					$"{this.settings.installFolder}/Modding API.zip",
					this.settings.temp
				);
				File.Delete($"{this.settings.installFolder}/Modding API.zip");
				return true;
			}
			return false;
		}

		public void SearchInstalledFiles(InstallResultListener callback)
		{
			DirectoryInfo modsFolder = new DirectoryInfo(this.settings.modFolder);
			FileInfo[] modsFiles = modsFolder.GetFiles("*.dll");

			if (!Directory.Exists($"{this.settings.modFolder}/Disabled"))
				Directory.CreateDirectory($"{this.settings.modFolder}/Disabled");

			DirectoryInfo disabledFolder = new DirectoryInfo($"{this.settings.modFolder}/Disabled");
			FileInfo[] disabledFiles = disabledFolder.GetFiles("*.dll");

			foreach (FileInfo modsFile in modsFiles)
			{
				bool isGDriveMod = this.ModsList.Any(m => m.Files.Keys.Contains(Path.GetFileName(modsFile.Name)));

				Mod mod;
				if (isGDriveMod)
				{
					mod = this.ModsList.First(m => m.Files.Keys.Contains(Path.GetFileName(modsFile.Name)));
				}
				else
				{
					mod = new Mod(Path.GetFileNameWithoutExtension(modsFile.Name), new Dictionary<string, string>{[Path.GetFileName(modsFile.Name)] = Util.GetSHA1(modsFile.FullName)});
				}

				if (string.IsNullOrEmpty(mod.Name) || this.allMods.Contains(mod.Name))
					continue;

				ModEntry entry = new ModEntry
				{
					Name = mod.Name,
					IsEnabled = true,
					IsInstalled = true
				};

				this.modsList.Add(mod);
				this.allMods.Add(mod.Name);
				this.installedMods.Add(mod.Name);
				this.modEntries.Add(callback.Register(entry));
			}

			foreach (FileInfo file in disabledFiles)
			{
				bool isGDriveMod = this.modsList.Any(m => m.Files.Keys.Contains(Path.GetFileName(file.Name)));

				Mod mod;
				if (isGDriveMod)
				{
					mod = this.modsList.First(m => m.Files.Keys.Contains(Path.GetFileName(file.Name)));
				}
				else
				{
					mod = new Mod(Path.GetFileNameWithoutExtension(file.Name), new Dictionary<string, string>{[Path.GetFileName(file.Name)] = Util.GetSHA1(file.FullName)});
				}

				if (string.IsNullOrEmpty(mod.Name) || this.allMods.Contains(mod.Name))
					continue;

				ModEntry entry = new ModEntry
				{
					Name = mod.Name,
					IsEnabled = true,
					IsInstalled = true
				};

				entry.Name = mod.Name;

				this.modsList.Add(mod);
				this.allMods.Add(mod.Name);
				this.installedMods.Add(mod.Name);
				this.modEntries.Add(callback.Register(entry));
			}

			foreach ((FileInfo file, bool enabled) in modsFiles.Select(x => (x, true)).Union(disabledFiles.Select(x => (x, false))))
			{
				// High-key hate having to do it again but i cba refactoring the entire method
				// If it's in the modlinks
				Mod? mod = this.modsList.Cast<Mod?>().FirstOrDefault(x => x is Mod m && m.Files.Keys.Contains(Path.GetFileName(file.Name)));

				if (!(mod is Mod modlinksMod)) continue;

				if (!CheckModUpdated(file.FullName, modlinksMod, enabled))
				{
					InstallDependencies(modlinksMod, callback);
				}
			}
		}

		private void LoadModLinks()
		{
			XDocument modLinks = XDocument.Load(ModLinks);
			XElement xElements = modLinks.Element("ModLinks");

			this.installer = new Installer(this.settings.APIFolder, xElements?.Element("Installer"));
			this.vanilla = new Vanilla(this.settings.APIFolder, xElements?.Element("Vanilla"), Manager.OS);
			XElement[] mods = xElements?.Element("ModList")?.Elements("ModLink").ToArray();
			if (mods == null) return;

			foreach (XElement mod in mods)
			{
				switch (mod.Element("Name")?.Value)
				{
					case "Modding API":
						this.api = new Api(this.settings.installFolder, mod, Manager.OS);
						break;
					default:
						this.modsList.Add(new Mod(mod));
						break;
				}
			}
		}

	#endregion

		// FIXME : This feature is duplicated below. Refactor client code and elimitante this one.
		public void CheckTemporary()
		{
			if (Directory.Exists("/tmp"))
			{
				if (Directory.Exists("/tmp/HKmodinstaller"))
				{
					Util.DeleteDirectory("/tmp/HKmodinstaller");
				}

				Directory.CreateDirectory("/tmp/HKmodinstaller");
				this.settings.temp = "/tmp/HKmodinstaller";
			}
			else
			{
				this.settings.temp =
					Directory.Exists($"{Path.GetPathRoot(this.settings.installFolder)}temp")
						? $"{Path.GetPathRoot(this.settings.installFolder)}tempMods"
						: $"{Path.GetPathRoot(this.settings.installFolder)}temp";
			}

			this.settings.Save();
		}

		internal void CheckTemporary(string d)
		{
			// If user is on sane operating system with a /tmp folder, put temp files here.
			// Reasoning:
			// 1) /tmp usually has normal user write permissions. C:\temp might not.
			// 2) /tmp is usually on a ramdisk. Less disk writing is always better.
			if (Directory.Exists($"{d}tmp"))
			{
				if (Directory.Exists($"{d}tmp/HKmodinstaller"))
				{
					Util.DeleteDirectory($"{d}tmp/HKmodinstaller");
				}

				Directory.CreateDirectory($"{d}tmp/HKmodinstaller");
				this.settings.temp = $"{d}tmp/HKmodinstaller";
			}
			else
			{
				this.settings.temp = Directory.Exists($"{d}temp")
					? $"{d}tempMods"
					: $"{d}temp";
			}
		}

		#region Downloading and installing

		public void InstallDependencies(Mod mod, InstallResultListener callback)
		{
			if (!mod.Dependencies.Any()) return;

			CheckApiInstalled(callback);

			foreach (string dependency in mod.Dependencies)
			{
				if (dependency == "Modding API")
					continue;

				if (this.InstalledMods.Contains(dependency))
					continue;

				ModEntry dependencyMod = this.ModEntries.First(dep => dep.Name == dependency);
				if (null == dependencyMod)
					throw new FileNotFoundException($"Could not find \"{dependency}\" which is required to run \"{mod.Name}\"!\r\nYou may need to install \"{dependency}\" manually.");

				Install(dependencyMod, true, false, true, callback);
			}
		}

		public void Install(ModEntry entry, bool isInstall, bool isUpdate, bool isEnabled, InstallResultListener callback)
		{
			Mod mod = this.modsList.First(m => m.Name == entry.Name);
			if (isInstall)
			{
				if (!callback.Download(
					new Uri(mod.Link),
					$"{this.settings.modFolder}/{mod.Name}.zip",
					mod.Name
				))
					Util.Download(mod, this.settings.modFolder);

				InstallMods
				(
					$"{this.settings.modFolder}/{mod.Name}.zip",
					this.settings.temp,
					isEnabled
				);

				File.Delete($"{this.settings.modFolder}/{mod.Name}.zip");
			}
			else
			{
				string readmeModPathNoExtension = $"{this.settings.installFolder}/README({mod.Name})";
				string readmeModPathTxt = $"{readmeModPathNoExtension}.txt";
				string readmeModPathMd = $"{readmeModPathNoExtension}.md";

				foreach (string s in mod.Files.Keys)
				{
					if (File.Exists($"{this.settings.modFolder}/{s}"))
					{
						File.Delete($"{this.settings.modFolder}/{s}");
					}
				}

				foreach (string directory in Directory.EnumerateDirectories(this.settings.modFolder))
				{
					if (!Directory.EnumerateFileSystemEntries(directory).Any() && directory != "Disabled")
						Directory.Delete(directory);
				}

				if (File.Exists(readmeModPathTxt))
				{
					File.Delete(readmeModPathTxt);
				}
				else if (File.Exists(readmeModPathMd))
				{
					File.Delete(readmeModPathMd);
				}

				this.InstalledMods.Remove(mod.Name);
			}

			entry.IsInstalled = isInstall;
			entry.IsEnabled = entry.IsInstalled && isEnabled;

			if (isInstall)	callback.InstalledWithSuccess(entry);
			if (isUpdate)	callback.UpdatedWithSuccess(entry);
			else			callback.UninstalledWithSuccess(entry);
		}

		public void Uninstall(ModEntry entry, InstallResultListener callback) => Install(entry, false, false, false, callback);

		public bool CheckModUpdated(string filename, Mod mod, bool isEnabled)
		{
			return Util.SHA1Equals (
				filename,
				mod.Files[mod.Files.Keys.First(f => f == Path.GetFileName(filename))]
			);
		}

		public void UpdateMod(Mod mod, bool isEnabled, ModEntry updateMod, InstallResultListener callback)
		{
			this.InstallDependencies(mod, callback);
			this.Install(updateMod, true, true, isEnabled, callback);
		}

		public void OpenReadMe(ModEntry entry)
		{
			Mod mod = this.ModsList.First(m => m.Name == entry.Name);
			string modName = mod.Name;

			// The only two possible options are .txt or .md, which follows from the InstallMods method
			// The same method also describes, the way all readme files are formatted.
			string readmeModPathNoExtension = $"{this.settings.installFolder}/README({modName})";
			string readmeModPathTxt = $"{readmeModPathNoExtension}.txt";
			string readmeModPathMd = $"{readmeModPathNoExtension}.md";

			// If a readme is created, open it using the default application.
			if (File.Exists(readmeModPathTxt))
			{
				Util.Open(readmeModPathTxt);
			}
			else if (File.Exists(readmeModPathMd))
			{
				try
				{
					Util.Open(readmeModPathMd);
				}
				catch
				{
					string tempReadme = Path.GetTempPath() + "HKModInstallerTempReadme.txt";
					File.Copy(readmeModPathMd, tempReadme, true);
					Util.Open(tempReadme);
				}
			}
			else
			{
				throw new FileNotFoundException($"No readme exists for {modName}.");
			}
		}

		public void Enable(ModEntry entry)
		{
			Mod mod = this.ModsList.First(m => m.Name == entry.Name);
			string modName = mod.Name;

			if (this.ModsList.Any(m => m.Name == modName))
			{
				foreach (string s in this.ModsList.First(m => m.Name == modName)
											  .Files.Keys
											  .Where(f => Path.GetExtension(f) == ".dll"))
				{
					if (!File.Exists($"{this.settings.modFolder}/Disabled/{s}")) continue;
					if (File.Exists($"{this.settings.modFolder}/{s}"))
					{
						File.Delete($"{this.settings.modFolder}/{s}");
					}

					File.Move
					(
						$"{this.settings.modFolder}/Disabled/{s}",
						$"{this.settings.modFolder}/{s}"
					);
				}
			}
			else
			{
				if (!File.Exists($"{this.settings.modFolder}/Disabled/{modName}")) return;
				if (File.Exists($"{this.settings.modFolder}/{modName}"))
				{
					File.Delete($"{this.settings.modFolder}/{modName}");
				}

				File.Move
				(
					$"{this.settings.modFolder}/Disabled/{modName}",
					$"{this.settings.modFolder}/{modName}"
				);
			}
		}

		public void Disable(ModEntry entry)
		{
			Mod mod = this.ModsList.First(m => m.Name == entry.Name);
			string modName = mod.Name;

			if (this.ModsList.Any(m => m.Name == modName))
			{
				foreach (string s in this.ModsList.First(m => m.Name == modName)
											  .Files.Keys
											  .Where(f => Path.GetExtension(f) == ".dll"))
				{
					if (!File.Exists($"{this.settings.modFolder}/{s}")) continue;
					if (File.Exists($"{this.settings.modFolder}/Disabled/{s}"))
					{
						File.Delete($"{this.settings.modFolder}/Disabled/{s}");
					}

					File.Move
					(
						$"{this.settings.modFolder}/{s}",
						$"{this.settings.modFolder}/Disabled/{s}"
					);
				}
			}
			else
			{
				if (!File.Exists($"{this.settings.modFolder}/{modName}")) return;
				if (File.Exists($"{this.settings.modFolder}/Disabled/{modName}"))
				{
					File.Delete($"{this.settings.modFolder}/Disabled/{modName}");
				}

				File.Move
				(
					$"{this.settings.modFolder}/{modName}",
					$"{this.settings.modFolder}/Disabled/{modName}"
				);
			}
		}

		#region Unpacking and moving/copying/deleting files

		private void InstallApi(string api, string tempFolder)
		{
			// Check if first time installation.
			// If an accident happens below, we can have an inconsistency between .dll, .vanilla and .mod.
			// The .bkp ensures us we will always have a printine DLL to restart over.
			// (yeah, I messed up my game - this is why I'm working on this now! =] )
			if (!File.Exists($"{this.settings.APIFolder}/Assembly-CSharp.dll.bkp"))
			{
				if (!this.vanilla.IsEnabled) throw new InvalidOperationException(
					"The Assembly-CSharp.dll is not the original and no backup was found. Your installment is inconsistent.\n"
					+"Reinstall or Repair the game and try again."
				);
				File.Copy
				(
					$"{this.settings.APIFolder}/Assembly-CSharp.dll",
					$"{this.settings.APIFolder}/Assembly-CSharp.bkp",
					false
				);
			}
			{
				FastZip zipFile = new FastZip();
				zipFile.ExtractZip(api, tempFolder, "*");
			}
			IEnumerable<string> mods = Directory.EnumerateDirectories(tempFolder).ToList();
			IEnumerable<string> files = Directory.EnumerateFiles(tempFolder).ToList();
			if (this.AssemblyIsAPI)
			{
				File.Copy
				(
					$"{this.settings.APIFolder}/Assembly-CSharp.dll",
					$"{this.settings.APIFolder}/Assembly-CSharp.vanilla",
					true
				);
			}

			if (!files.Any(f => f.Contains(".dll")))
			{
				string[] modDll = Directory.GetFiles(tempFolder, "*.dll", SearchOption.AllDirectories);
				foreach (string dll in modDll)
					File.Copy(dll, $"{this.settings.APIFolder}/{Path.GetFileName(dll)}", true);
				foreach (string mod in mods)
				{
					string[] dll = Directory.GetFiles(mod, "*.dll", SearchOption.AllDirectories);
					if (dll.Length == 0)
					{
						Util.MoveDirectory(mod, $"{this.settings.installFolder}/{Path.GetFileName(mod)}/");
					}
				}

				foreach (string file in files)
				{
					File.Copy
					(
						file,
						$"{this.settings.installFolder}/{Path.GetFileNameWithoutExtension(file)}({Path.GetFileNameWithoutExtension(api)}){Path.GetExtension(file)}",
						true
					);
					File.Delete(file);
				}

				Directory.Delete(tempFolder, true);
			}
			else
			{
				foreach (string file in files)
				{
					File.Copy
					(
						file,
						file.Contains("*.txt")
							? $"{this.settings.installFolder}/{Path.GetFileNameWithoutExtension(file)}({Path.GetFileNameWithoutExtension(api)}){Path.GetExtension(file)}"
							: $"{this.settings.modFolder}/{Path.GetFileName(file)}",
						true
					);
					File.Delete(file);
				}

				Directory.Delete(tempFolder, true);
			}

			this.ApiIsInstalled = true;
			this.settings.Save();
		}

		public void InstallMods(string mod, string tempFolder, bool isEnabled)
		{
			if (Directory.Exists(this.settings.temp))
				Directory.Delete(tempFolder, true);
			if (!Directory.Exists(this.settings.modFolder))
				Directory.CreateDirectory(this.settings.modFolder);

			{
				FastZip zipFile = new FastZip();
				zipFile.ExtractZip(mod, tempFolder, "*");
			}

			List<string> files = Directory.EnumerateFiles(tempFolder, "*", SearchOption.AllDirectories).ToList();

			foreach (string file in files)
			{
				switch (Path.GetExtension(file))
				{
					case ".dll":
						File.Copy
						(
							file,
							isEnabled
								? $"{this.settings.modFolder}/{Path.GetFileName(file)}"
								: $"{this.settings.modFolder}/Disabled/{Path.GetFileName(file)}",
							true
						);
						break;
					case ".txt":
					case ".md":
						File.Copy
						(
							file,
							$"{this.settings.installFolder}/{Path.GetFileNameWithoutExtension(file)}({Path.GetFileNameWithoutExtension(mod)}){Path.GetExtension(file)}",
							true
						);
						break;
					case ".ini":
						break;
					default:
						string path = Path.GetDirectoryName(file)
										  ?.Replace
										  (
											  this.settings.temp,
											  this.settings.installFolder
										  );
						if (!Directory.Exists(path))
							if (path != null)
								Directory.CreateDirectory(path);
						File.Copy(file, $"{path}/{Path.GetFileName(file)}", true);
						break;
				}
			}

			Directory.Delete(tempFolder, true);

			this.installedMods.Add(mod);
		}

		#endregion

		#endregion

		public bool SetVanillaApi()
		{
			if (File.Exists($"{this.settings.APIFolder}/Assembly-CSharp.vanilla"))
			{
				File.Copy
				(
					$"{this.settings.APIFolder}/Assembly-CSharp.dll",
					$"{this.settings.APIFolder}/Assembly-CSharp.mod",
					true
				);
				File.Copy
				(
					$"{this.settings.APIFolder}/Assembly-CSharp.vanilla",
					$"{this.settings.APIFolder}/Assembly-CSharp.dll",
					true
				);
				return true;
			}
			return false;
		}

		public bool SetCustomApi()
		{
			if (File.Exists($"{this.settings.APIFolder}/Assembly-CSharp.mod"))
			{
				File.Copy
				(
					$"{this.settings.APIFolder}/Assembly-CSharp.dll",
					$"{this.settings.APIFolder}/Assembly-CSharp.vanilla",
					true
				);
				File.Copy
				(
					$"{this.settings.APIFolder}/Assembly-CSharp.mod",
					$"{this.settings.APIFolder}/Assembly-CSharp.dll",
					true
				);
				return true;
			}
			return false;
		}

 		public bool PathCheck(string path)
		{
			return _OS == "MacOS"
				? File.Exists
				(
					path + "/Contents/Resources/Data/Managed/Assembly-CSharp.dll"
				)
				&& new[] { "hollow_knight.app", "Hollow Knight.app" }.Contains(Path.GetFileName(path))
				: File.Exists(path + "/hollow_knight_Data/Managed/Assembly-CSharp.dll");
		}
	}
}