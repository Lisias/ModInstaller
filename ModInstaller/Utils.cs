using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ModInstaller
{
	internal static class Util
	{
		static Util()
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		}

		internal static string GetCurrentOS()
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

		internal static bool SHA1Equals(string file, string modmd5) => string.Equals(GetSHA1(file), modmd5, StringComparison.InvariantCultureIgnoreCase);

		internal static string GetSHA1(string file)
		{
			using (SHA1 sha1 = SHA1.Create())
			using (FileStream stream = File.OpenRead(file))
			{
				byte[] hash = sha1.ComputeHash(stream);
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

		internal static void DeleteDirectory(string targetDir)
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

		internal static void MoveDirectory(string source, string target)
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

		internal static void DownloadFile(Installer installer, string targetDir)
		{
			WebClient dl = new WebClient();
			dl.DownloadFile
			(
				new Uri
				(
					installer?.AULink ?? throw new NoNullAllowedException("AULink Missing!")
				),
				$"{targetDir}/AU.exe"
			);
		}

		internal static void DownloadFile(Api api, string targetDir)
		{
			WebClient dl = new WebClient();
			dl.DownloadFile
			(
				new Uri
				(
					api?.Link ?? throw new NoNullAllowedException("Link Missing!")
				),
				$"{targetDir}/Modding API.zip"
			);
		}

		internal static void Download(Mod mod, string targetDir)
		{
			WebClient dl = new WebClient();
			dl.DownloadFile
			(
				new Uri
				(
					mod.Link ?? throw new NoNullAllowedException("Link Missing!")
				),
				$"{targetDir}/{mod.Name}.zip"
			);
		}

		internal static void Execute(Installer installer, string dir, string file)
		{
			Process process = new Process
			{
				StartInfo =
				{
					FileName = $"{dir}/AU.exe",
					Arguments = $"\"{dir}\" {installer.Link} {file}"
				}
			};
			process.Start();
		}

		internal static void Open(string path)
		{
			Process.Start(path);
		}

	}
}
