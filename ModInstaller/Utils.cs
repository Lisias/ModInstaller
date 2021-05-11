using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using ICSharpCode.SharpZipLib.Zip;

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

		internal static void DirectoryDeleteSafely(string victim)
		{
			if (Directory.Exists(victim)) DeleteDirectory(victim);
		}

		internal static void DirectoryCreateSafely(string pathname)
		{
			if (!Directory.Exists(pathname)) Directory.CreateDirectory(pathname);
		}

		internal static void DirectoryRecreate(string victim)
		{
			if (Directory.Exists(victim)) DeleteDirectory(victim);
			Directory.CreateDirectory(victim);
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
				DirectoryRecreate(targetFolder);
				foreach (string file in folder)
				{
					string targetFile = Path.Combine
					(
						targetFolder,
						Path.GetFileName(file) ?? throw new NoNullAllowedException("File name is null!")
					);
					File.Move(file, targetFile);
				}
			}

			DeleteDirectory(source);
		}

		internal static void FileDeleteSafely(string victim)
		{
			if (File.Exists(victim)) File.Delete(victim);
		}

		internal static void Download(Installer installer, string targetDir, Manager.InstallResultListener callback)
		{
			Uri uri = new Uri(installer?.AULink ?? throw new NoNullAllowedException("AULink Missing!"));
			string pathname = $"{targetDir}/package.zip";
			DownloadProgressListener listener = callback.Download (
					uri,
					pathname,
					"Modding API"
				);
			if (null != listener)
				new DownloadFile(
					uri
					, pathname
					, listener
				).Do();
		}

		internal static void Download(Api api, string targetDir, Manager.InstallResultListener callback)
		{
			Uri uri = new Uri(api.Link ?? throw new NoNullAllowedException("Link Missing!"));
			string pathname = $"{targetDir}/Modding API.zip";
			DownloadProgressListener listener = callback.Download (
					uri,
					pathname,
					"Modding API.zip"
				);
			if (null != listener)
				new DownloadFile(
					uri
					, pathname
					, listener
			).Do();
		}

		internal static void Download(Mod mod, string targetDir, Manager.InstallResultListener callback)
		{
			Uri uri = new Uri(mod.Link ?? throw new NoNullAllowedException("Link Missing!"));
			string pathname = $"{targetDir}/{mod.Name}.zip";

			DownloadProgressListener listener = callback.Download (
					uri,
					pathname,
					$"{mod.Name}.zip"
				);
			if (null != listener)
				new DownloadFile(
					uri
					, pathname
					, listener
			).Do();
		}

		internal static void Execute(Installer installer, string dir, string file)
		{
			Unzip($"{dir}/package.zip", dir);
			Process process = new Process
			{
				StartInfo =
				{
					FileName = $"{dir}/ModInstallerAutoUpdater.exe",
					Arguments = $"\"{dir}\" {installer.Link} {file}"
				}
			};
			process.Start();
		}

		internal static void Open(string path)
		{
			Process.Start(path);
		}

		internal static void Unzip(string pathname, string target)
		{
			FastZip zipFile = new FastZip();
			zipFile.ExtractZip(pathname, target, "*");
		}
	}

	internal class DownloadFile
	{
		private readonly Uri uri;
		private readonly string path;
		private readonly DownloadProgressListener listener;

		private readonly Stopwatch sw = new Stopwatch();
		private long size = 0;

		internal DownloadFile(Uri uri, string path, DownloadProgressListener listener)
		{
			this.uri = uri;
			this.path = path;
			this.listener = listener;
		}

		internal void Do()
		{
			this.sw.Reset();
			this.sw.Start();
			using (WebClient dl = new WebClient())
			{
				dl.DownloadProgressChanged += this.DownloadProgressChangedHandler;
				dl.DownloadFileCompleted += this.DownloadFileCompletedHandler;
				dl.DownloadFileAsync(uri, path);
			}
		}

		private void DownloadFileCompletedHandler(object sender, AsyncCompletedEventArgs e)
		{
			this.sw.Stop();
			if (e.Cancelled) this.listener.Cancelled();
			if (null != e.Error) this.listener.Aborted(e.Error);
			this.listener.Completed(this.size, this.sw.ElapsedMilliseconds);
		}

		private void DownloadProgressChangedHandler(object sender, DownloadProgressChangedEventArgs e)
		{
			this.listener.Progress(e.BytesReceived, e.TotalBytesToReceive, this.sw.ElapsedMilliseconds);
		}
	}
}
