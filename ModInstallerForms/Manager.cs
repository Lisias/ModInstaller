using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Security;
using System.Linq;
using System.IO.Compression;

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
			void Download(Uri uri, string path, string name);
			void InstalledWithSuccess(string modname);
			void UpdatedWithSuccess(string modname);
			void UninstalledWithSuccess(string modname);
			void Update(ModEntry entry);
			ModEntry Register(ModEntry entry);
		}

		private const string ModLinks = "https://raw.githubusercontent.com/Ayugradow/ModInstaller/master/modlinks.xml";

		public const string Version = "v8.7.2 /L";
		public const string Author = "Lisias";

		public struct Mod
		{
			public string Name { get; set; }
			public Dictionary<string, string> Files { get; set; }
			public string Link { get; set; }
			public List<string> Dependencies { get; set; }
			public List<string> Optional { get; set; }
		}

		public class ModEntry
		{
			public string Name { get; set; }
			public bool IsInstalled { get; set; }
			public bool IsEnabled { get; set; }
		} 

		private static string _OS;
		public static string OS => _OS ??= GetCurrentOS();

		public static string OSPath => 
			OS == "MacOS"
				? $"{Properties.Settings.Default.installFolder}/Contents/Resources/Data/Managed"
				: $"{Properties.Settings.Default.installFolder}/hollow_knight_Data/Managed";

		private readonly List<string> defaultPaths = new();
		public List<string> DefaultPaths => defaultPaths;

		private readonly List<string> allMods = new();
		public List<string> AllMods => allMods;
 
		private readonly List<string> installedMods = new();
		public List<string>InstalledMods=> installedMods;

		private readonly List<Mod> modsList = new();
		public List<Mod> ModsList => modsList;
		public List<Mod> ModsSortedList => modsList.OrderBy(mod => mod.Name).ToList();

		private readonly List<ModEntry> modEntries = new();
		public List<ModEntry> ModEntries => modEntries;

		public bool ApiIsInstalled { get; private set; }
		public bool VanillaEnabled { get; private set; }
		public bool AssemblyIsAPI { get; private set; }
		public string CurrentPatch { get; private set; }

        private string _apiLink;

        private string _apiSha1;

		private static Manager instance;
		public static Manager Instance => instance ??= new Manager();
		private Manager()
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

			#if !DEBUG
				CheckUpdate();
			#endif

			this.FillDefaultPaths();
			this.FillModsList();

			this.VanillaEnabled = !SHA1Equals(Properties.Settings.Default.APIFolder + "/Assembly-CSharp.dll", _apiSha1);
		}

		public static void CheckUpdate()
		{
			string dir = AppDomain.CurrentDomain.BaseDirectory;
			string file = Path.GetFileName(Assembly.GetEntryAssembly()?.Location);
			XDocument dllist = XDocument.Load(ModLinks);

			if (File.Exists($"{dir}/lol.exe"))
				File.Delete($"{dir}/lol.exe");

			if (File.Exists($"{dir}/AU.exe"))
				File.Delete($"{dir}/AU.exe");

			XElement installer = dllist.Element("ModLinks")?.Element("Installer");

			// If the SHA1s are non-equal, update.
			if (installer == null || SHA1Equals($"{dir}/{file}", installer.Element("SHA1")?.Value)) return;

			WebClient dl = new WebClient();
			dl.DownloadFile
			(
				new Uri
				(
					installer.Element("AULink")?.Value ?? throw new NoNullAllowedException("AULink Missing!")
				),
				$"{dir}/AU.exe"
			);

			Process process = new Process
			{
				StartInfo =
				{
					FileName = $"{dir}/AU.exe",
					Arguments = $"\"{dir}\" {installer.Element("Link")?.Value} {file}"
				}
			};
			process.Start();
		}

		private static string GetCurrentOS()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return "Windows";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				return "MacOS";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				return "Linux";
			else
				return "Windows";
		}

		internal void SetInstallationPath(string selectedPath)
		{
			Properties.Settings.Default.installFolder = selectedPath;
			Properties.Settings.Default.APIFolder = Manager.OSPath;
			Properties.Settings.Default.modFolder = $"{Properties.Settings.Default.APIFolder}/Mods";
			Properties.Settings.Default.Save();
			if (!Directory.Exists(Properties.Settings.Default.modFolder))
				Directory.CreateDirectory(Properties.Settings.Default.modFolder);
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
					this.defaultPaths.Add
						(Environment.GetEnvironmentVariable("HOME") + "/Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app");
					break;
			}
		}

		public void CheckLocalInstallation(InstallationPathListener installationPathCallback)
		{
			if (string.IsNullOrEmpty(Properties.Settings.Default.installFolder))
			{
				DriveInfo[] allDrives = DriveInfo.GetDrives();

				foreach (DriveInfo d in allDrives.Where(d => d.DriveType == DriveType.Fixed))
				{
					foreach (string path in this.defaultPaths)
					{
						if (!Directory.Exists($"{d.Name}{path}")) continue;
						SetDefaultPath($"{d.Name}{path}", installationPathCallback);

						// If user is on sane operating system with a /tmp folder, put temp files here.
						// Reasoning:
						// 1) /tmp usually has normal user write permissions. C:\temp might not.
						// 2) /tmp is usually on a ramdisk. Less disk writing is always better.
						if (Directory.Exists($"{d.Name}tmp"))
						{
							if (Directory.Exists($"{d.Name}tmp/HKmodinstaller"))
							{
								DeleteDirectory($"{d.Name}tmp/HKmodinstaller");
							}

							Directory.CreateDirectory($"{d.Name}tmp/HKmodinstaller");
							Properties.Settings.Default.temp = $"{d.Name}tmp/HKmodinstaller";
						}
						else
						{
							Properties.Settings.Default.temp = Directory.Exists($"{d.Name}temp")
								? $"{d.Name}tempMods"
								: $"{d.Name}temp";
						}

						if (!string.IsNullOrEmpty(Properties.Settings.Default.installFolder))
							break;
						Properties.Settings.Default.Save();
					}

					if (!string.IsNullOrEmpty(Properties.Settings.Default.installFolder))
						break;
				}

				if (string.IsNullOrEmpty(Properties.Settings.Default.installFolder))
				{
					installationPathCallback.SetInstallationPathManually();
				}
				else
				{
					Properties.Settings.Default.APIFolder = OSPath;
					Properties.Settings.Default.modFolder = $"{Properties.Settings.Default.APIFolder}/Mods";
					Properties.Settings.Default.Save();
				}
			}

			if (!Directory.Exists(Properties.Settings.Default.modFolder))
			{
				Directory.CreateDirectory(Properties.Settings.Default.modFolder);
			}
		}

		private static void SetDefaultPath(string path, InstallationPathListener callback)
		{
			if (!callback.IsInstallationPath(path)) return;

			Properties.Settings.Default.installFolder = path;
			Properties.Settings.Default.Save();
		}

		public static void PiracyCheck()
		{
			if
			(
				OS != "Windows"
				|| File.Exists($"{Properties.Settings.Default.installFolder}/Galaxy.dll")
				|| Path.GetFileName(Properties.Settings.Default.installFolder) != "Hollow Knight Godmaster"
			)
				return;

			Process.Start("https://store.steampowered.com/app/367520/Hollow_Knight/");
			throw new VerificationException("Please purchase the game before attempting to play it.");
		}

		public bool CheckApiInstalled(InstallResultListener callback)
		{
			if (!Directory.Exists(Properties.Settings.Default.APIFolder))
			{
				// Make sure to not ruin everything forever
				Properties.Settings.Default.installFolder = null;

				throw new VerificationException("Folder does not exist! (Game is probably not installed) Exiting.");
			}

			// Check if either API is installed or if vanilla dll still exists
			if (File.Exists($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll"))
			{
				byte[] bytes = File.ReadAllBytes($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll");
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

				this.ApiIsInstalled = this.AssemblyIsAPI || File.Exists($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.mod");

				if (!File.Exists($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.vanilla")
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

			if (!this.ApiIsInstalled || this.AssemblyIsAPI && !SHA1Equals($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll", _apiSha1))
			{
				callback.Download
				(
					new Uri(_apiLink),
					$"{Properties.Settings.Default.installFolder}/Modding API.zip",
					"Modding API"
				);
				InstallApi
				(
					$"{Properties.Settings.Default.installFolder}/Modding API.zip",
					Properties.Settings.Default.temp
				);
				File.Delete($"{Properties.Settings.Default.installFolder}/Modding API.zip");
				return true;
			}
			return false;
		}

		public void SearchInstalledFiles(InstallResultListener callback)
		{
			DirectoryInfo modsFolder = new DirectoryInfo(Properties.Settings.Default.modFolder);
			FileInfo[] modsFiles = modsFolder.GetFiles("*.dll");

			if (!Directory.Exists($"{Properties.Settings.Default.modFolder}/Disabled"))
				Directory.CreateDirectory($"{Properties.Settings.Default.modFolder}/Disabled");

			DirectoryInfo disabledFolder = new DirectoryInfo($"{Properties.Settings.Default.modFolder}/Disabled");
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
					mod = new Mod
					{
						Name = Path.GetFileNameWithoutExtension(modsFile.Name),
						Files = new Dictionary<string, string>
						{
							[Path.GetFileName(modsFile.Name)] = GetSHA1(modsFile.FullName)
						},
						Link = "",
						Dependencies = new List<string>(),
						Optional = new List<string>()
					};
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
					mod = new Mod
					{
						Name = Path.GetFileNameWithoutExtension(file.Name),
						Files = new Dictionary<string, string> { [Path.GetFileName(file.Name)] = GetSHA1(file.FullName) },
						Link = "",
						Dependencies = new List<string>(),
						Optional = new List<string>()
					};
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


		public void FillModsList()
		{
			XDocument dllist = XDocument.Load(ModLinks);
			XElement[] mods = dllist.Element("ModLinks")?.Element("ModList")?.Elements("ModLink").ToArray();

			if (mods == null) return;

			foreach (XElement mod in mods)
			{
				switch (mod.Element("Name")?.Value)
				{
					case "Modding API":
						_apiLink = OS == "Windows" ? mod.Element("Link")?.Value : mod.Element("UnixLink")?.Value;
						_apiSha1 = mod.Element("Files")?.Element("File")?.Element("SHA1")?.Value;
						this.CurrentPatch = mod.Element("Files")?.Element("File")?.Element("Patch")?.Value;
						break;
					default:
						this.modsList.Add
						(
							new Mod
							{
								Name = mod.Element("Name")?.Value,
								Link = mod.Element("Link")?.Value,
								Files = mod.Element("Files")
										   ?.Elements("File")
										   .ToDictionary
										   (
											   element => element.Element("Name")?.Value,
											   element => element.Element("SHA1")?.Value
										   ),
								Dependencies = mod.Element("Dependencies")
												  ?.Elements("string")
												  .Select(dependency => dependency.Value)
												  .ToList(),
								Optional = mod.Element("Optional")
											  ?.Elements("string")
											  .Select(dependency => dependency.Value)
											  .ToList()
									?? new List<string>()
							}
						);
						break;
				}
			}
		}

		internal static void CheckTemporary()
		{
			if (Directory.Exists("/tmp"))
			{
				if (Directory.Exists("/tmp/HKmodinstaller"))
				{
					Manager.DeleteDirectory("/tmp/HKmodinstaller");
				}

				Directory.CreateDirectory("/tmp/HKmodinstaller");
				Properties.Settings.Default.temp = "/tmp/HKmodinstaller";
			}
			else
			{
				Properties.Settings.Default.temp =
					Directory.Exists($"{Path.GetPathRoot(Properties.Settings.Default.installFolder)}temp")
						? $"{Path.GetPathRoot(Properties.Settings.Default.installFolder)}tempMods"
						: $"{Path.GetPathRoot(Properties.Settings.Default.installFolder)}temp";
			}

			Properties.Settings.Default.Save();
		}

		private static void DeleteDirectory(string targetDir)
		{
			string[] files = Directory.GetFiles(targetDir);
			string[] dirs = Directory.GetDirectories(targetDir);

			foreach (string file in files)
			{
				File.SetAttributes(file, FileAttributes.Normal);
				File.Delete(file);
			}

			foreach (string dir in dirs)
			{
				DeleteDirectory(dir);
			}

			Directory.Delete(targetDir, false);
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

				var dependencyMod = this.ModEntries.First(dep => dep.Name == dependency);

				Install(dependency, true, false, true, dependencyMod, callback);
			}
		}

		public void Install(string modname, bool isInstall, bool isUpdate, bool isEnabled, ModEntry entry, InstallResultListener callback)
		{
			if (isInstall)
			{
				callback.Download
				(
					new Uri(this.modsList.First(m => m.Name == modname).Link),
					$"{Properties.Settings.Default.modFolder}/{modname}.zip",
					modname
				);

				InstallMods
				(
					$"{Properties.Settings.Default.modFolder}/{modname}.zip",
					Properties.Settings.Default.temp,
					isEnabled
				);

				File.Delete($"{Properties.Settings.Default.modFolder}/{modname}.zip");

				if (isUpdate)	callback.UpdatedWithSuccess(modname);
				else			callback.InstalledWithSuccess(modname);
			}
			else
			{
				Mod mod = this.modsList.First(m => m.Name == entry.Name);

				string readmeModPathNoExtension = $"{Properties.Settings.Default.installFolder}/README({mod.Name})";
				string readmeModPathTxt = $"{readmeModPathNoExtension}.txt";
				string readmeModPathMd = $"{readmeModPathNoExtension}.md";

				foreach (string s in mod.Files.Keys)
				{
					if (File.Exists($"{Properties.Settings.Default.modFolder}/{s}"))
					{
						File.Delete($"{Properties.Settings.Default.modFolder}/{s}");
					}
				}

				foreach (string directory in Directory.EnumerateDirectories(Properties.Settings.Default.modFolder))
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

				callback.UninstalledWithSuccess(modname);
				this.InstalledMods.Remove(modname);
			}

			entry.IsInstalled = !entry.IsInstalled;
			entry.IsEnabled = entry.IsInstalled;
			callback.Update(entry);
		}

		public void Uninstall(string mod, ModEntry entry, InstallResultListener callback) => Install(mod, false, false, false, entry, callback);

		public bool CheckModUpdated(string filename, Manager.Mod mod, bool isEnabled)
		{
			return SHA1Equals (
				filename,
				mod.Files[mod.Files.Keys.First(f => f == Path.GetFileName(filename))]
			);
		}

		public void UpdateMod(Mod mod, bool isEnabled, ModEntry updateMod, InstallResultListener callback)
		{
			this.InstallDependencies(mod, callback);
			this.Install(mod.Name, true, true, isEnabled, updateMod, callback);
		}

		public void OpenReadMe(ModEntry entry)
		{
			Manager.Mod mod = Manager.Instance.ModsList.First(m => m.Name == entry.Name);
			string modName = mod.Name;

			// The only two possible options are .txt or .md, which follows from the InstallMods method
			// The same method also describes, the way all readme files are formatted.
			string readmeModPathNoExtension = $"{Properties.Settings.Default.installFolder}/README({modName})";
			string readmeModPathTxt = $"{readmeModPathNoExtension}.txt";
			string readmeModPathMd = $"{readmeModPathNoExtension}.md";

			// If a readme is created, open it using the default application.
			if (File.Exists(readmeModPathTxt))
			{
				Process.Start(readmeModPathTxt);
			}
			else if (File.Exists(readmeModPathMd))
			{
				try
				{
					Process.Start(readmeModPathMd);
				}
				catch
				{
					string tempReadme = Path.GetTempPath() + "HKModInstallerTempReadme.txt";
					File.Copy(readmeModPathMd, tempReadme, true);
					Process.Start(tempReadme);
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
					if (!File.Exists($"{Properties.Settings.Default.modFolder}/Disabled/{s}")) continue;
					if (File.Exists($"{Properties.Settings.Default.modFolder}/{s}"))
					{
						File.Delete($"{Properties.Settings.Default.modFolder}/{s}");
					}

					File.Move
					(
						$"{Properties.Settings.Default.modFolder}/Disabled/{s}",
						$"{Properties.Settings.Default.modFolder}/{s}"
					);
				}
			}
			else
			{
				if (!File.Exists($"{Properties.Settings.Default.modFolder}/Disabled/{modName}")) return;
				if (File.Exists($"{Properties.Settings.Default.modFolder}/{modName}"))
				{
					File.Delete($"{Properties.Settings.Default.modFolder}/{modName}");
				}

				File.Move
				(
					$"{Properties.Settings.Default.modFolder}/Disabled/{modName}",
					$"{Properties.Settings.Default.modFolder}/{modName}"
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
					if (!File.Exists($"{Properties.Settings.Default.modFolder}/{s}")) continue;
					if (File.Exists($"{Properties.Settings.Default.modFolder}/Disabled/{s}"))
					{
						File.Delete($"{Properties.Settings.Default.modFolder}/Disabled/{s}");
					}

					File.Move
					(
						$"{Properties.Settings.Default.modFolder}/{s}",
						$"{Properties.Settings.Default.modFolder}/Disabled/{s}"
					);
				}
			}
			else
			{
				if (!File.Exists($"{Properties.Settings.Default.modFolder}/{modName}")) return;
				if (File.Exists($"{Properties.Settings.Default.modFolder}/Disabled/{modName}"))
				{
					File.Delete($"{Properties.Settings.Default.modFolder}/Disabled/{modName}");
				}

				File.Move
				(
					$"{Properties.Settings.Default.modFolder}/{modName}",
					$"{Properties.Settings.Default.modFolder}/Disabled/{modName}"
				);
			}
		}

		#region Unpacking and moving/copying/deleting files

		private void InstallApi(string api, string tempFolder)
		{
			ZipFile.ExtractToDirectory(api, tempFolder);
			IEnumerable<string> mods = Directory.EnumerateDirectories(tempFolder).ToList();
			IEnumerable<string> files = Directory.EnumerateFiles(tempFolder).ToList();
			if (this.AssemblyIsAPI)
			{
				File.Copy
				(
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll",
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.vanilla",
					true
				);
			}

			if (!files.Any(f => f.Contains(".dll")))
			{
				string[] modDll = Directory.GetFiles(tempFolder, "*.dll", SearchOption.AllDirectories);
				foreach (string dll in modDll)
					File.Copy(dll, $"{Properties.Settings.Default.APIFolder}/{Path.GetFileName(dll)}", true);
				foreach (string mod in mods)
				{
					string[] dll = Directory.GetFiles(mod, "*.dll", SearchOption.AllDirectories);
					if (dll.Length == 0)
					{
						MoveDirectory(mod, $"{Properties.Settings.Default.installFolder}/{Path.GetFileName(mod)}/");
					}
				}

				foreach (string file in files)
				{
					File.Copy
					(
						file,
						$"{Properties.Settings.Default.installFolder}/{Path.GetFileNameWithoutExtension(file)}({Path.GetFileNameWithoutExtension(api)}){Path.GetExtension(file)}",
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
							? $"{Properties.Settings.Default.installFolder}/{Path.GetFileNameWithoutExtension(file)}({Path.GetFileNameWithoutExtension(api)}){Path.GetExtension(file)}"
							: $"{Properties.Settings.Default.modFolder}/{Path.GetFileName(file)}",
						true
					);
					File.Delete(file);
				}

				Directory.Delete(tempFolder, true);
			}

			this.ApiIsInstalled = true;
			Properties.Settings.Default.Save();
		}

		public void InstallMods(string mod, string tempFolder, bool isEnabled)
		{
			if (Directory.Exists(Properties.Settings.Default.temp))
				Directory.Delete(tempFolder, true);
			if (!Directory.Exists(Properties.Settings.Default.modFolder))
				Directory.CreateDirectory(Properties.Settings.Default.modFolder);

			ZipFile.ExtractToDirectory(mod, tempFolder);
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
								? $"{Properties.Settings.Default.modFolder}/{Path.GetFileName(file)}"
								: $"{Properties.Settings.Default.modFolder}/Disabled/{Path.GetFileName(file)}",
							true
						);
						break;
					case ".txt":
					case ".md":
						File.Copy
						(
							file,
							$"{Properties.Settings.Default.installFolder}/{Path.GetFileNameWithoutExtension(file)}({Path.GetFileNameWithoutExtension(mod)}){Path.GetExtension(file)}",
							true
						);
						break;
					case ".ini":
						break;
					default:
						string path = Path.GetDirectoryName(file)
										  ?.Replace
										  (
											  Properties.Settings.Default.temp,
											  Properties.Settings.Default.installFolder
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

		private static void MoveDirectory(string source, string target)
		{
			string sourcePath = source.TrimEnd('\\', ' ');
			string targetPath = target.TrimEnd('\\', ' ');
			IEnumerable<IGrouping<string, string>> files = Directory.EnumerateFiles
																	(
																		sourcePath,
																		"*",
																		SearchOption.AllDirectories
																	)
																	.GroupBy(Path.GetDirectoryName);

			foreach (IGrouping<string, string> folder in files)
			{
				string targetFolder = folder.Key.Replace(sourcePath, targetPath);
				Directory.CreateDirectory(targetFolder);
				foreach (string file in folder)
				{
					string targetFile = Path.Combine
					(
						targetFolder,
						Path.GetFileName(file) ?? throw new NoNullAllowedException("File name is null!")
					);
					if (File.Exists(targetFile))
					{
						if (!File.Exists($"{targetFolder}/{Path.GetFileName(targetFile)}.vanilla"))
						{
							File.Move(targetFile, $"{targetFolder}/{Path.GetFileName(targetFile)}.vanilla");
						}
						else
						{
							File.Delete(targetFile);
						}
					}

					File.Move(file, targetFile);
				}
			}

			Directory.Delete(source, true);
		}

		#endregion

		#endregion

		internal bool SetVanillaApi()
		{
			if (File.Exists($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.vanilla"))
			{
				File.Copy
				(
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll",
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.mod",
					true
				);
				File.Copy
				(
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.vanilla",
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll",
					true
				);
				Manager.Instance.AssemblyIsAPI = false;
				Manager.Instance.VanillaEnabled = true;
				return true;
			}
			return false;
		}

		internal bool SetCustomApi()
		{
			if (File.Exists($"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.mod"))
			{
				File.Copy
				(
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll",
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.vanilla",
					true
				);
				File.Copy
				(
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.mod",
					$"{Properties.Settings.Default.APIFolder}/Assembly-CSharp.dll",
					true
				);
				Manager.Instance.AssemblyIsAPI = true;
				return true;
			}
			return false;
		}

 		internal static bool PathCheck(string path)
		{
			return Manager.OS == "MacOS"
				? File.Exists
				(
					path + "/Contents/Resources/Data/Managed/Assembly-CSharp.dll"
				)
				&& new[] { "hollow_knight.app", "Hollow Knight.app" }.Contains(Path.GetFileName(path))
				: File.Exists(path + "/hollow_knight_Data/Managed/Assembly-CSharp.dll");
		}

		private static bool SHA1Equals(string file, string modmd5) => string.Equals(GetSHA1(file), modmd5, StringComparison.InvariantCultureIgnoreCase);

		private static string GetSHA1(string file)
		{
			using var sha1 = SHA1.Create();

			using FileStream stream = File.OpenRead(file);

			byte[] hash = sha1.ComputeHash(stream);

			return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
		}
	}
}
