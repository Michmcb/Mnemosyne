namespace Mnemosyne
{
	using CmdLineNet;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	public static class Program
	{
		public static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);
		public static async Task<int> Main(string[] args)
		{
			Dictionary<string, Verb> verbHandlers = new()
			{
				["backup"] = new(async (args) =>
				{
					if (BackupArgs.Parse(BackupArgs.GetReader().Read(args)).Ok(out var parsedArgs, out var errMsg))
					{
						return await RunBackup(parsedArgs);
					}
					else
					{
						Console.WriteLine(errMsg);
						Console.WriteLine("For help, type in \"help backup\"");
						return ExitCode.BadArguments;
					}
				}, () => HelpWriter.ConsoleWriteHelp(BackupArgs.GetReader().OrderedArguments, HelpSettings.Default), "Backs up files"),
			};
			verbHandlers.Add("help", new((args) =>
			{
				string? verb = args.FirstOrDefault();
				if (verb != null)
				{
					if (verbHandlers.TryGetValue(verb, out var handler))
					{
						handler.WriteHelp();
						return Task.FromResult(0);
					}
					else
					{
						Console.WriteLine("Unrecognized verb: " + verb);
					}
				}
				foreach (var vh in verbHandlers)
				{
					Console.WriteLine(string.Concat(vh.Key, ": ", vh.Value.Description));
				}
				return Task.FromResult(0);
			}, () => { }, "Shows help"));

			if (args.Length > 0)
			{
				if (verbHandlers.TryGetValue(args[0], out var handler))
				{
					return await handler.Execute(args.Skip(1));
				}
			}
			Console.WriteLine("Enter 'help' for help");
			return ExitCode.BadVerb;
		}
		private static PreppedDirectories PrepDirectories(string destination)
		{
			DateTimeOffset now = DateTimeOffset.Now;
			DateTimeOffset lastBackupTime = DateTimeOffset.MinValue;
			string? lastBackupDir = null;
			Directory.CreateDirectory(destination);
			List<(DateTimeOffset at, string dir)> inProgress = [];
			foreach (var dir in Directory.EnumerateDirectories(destination, "*.*", SearchOption.TopDirectoryOnly))
			{
				var dName = Path.GetFileName(dir.AsSpan());
				// 25 is the length of a datetime offset string, 33 is the length of the _ and GUID
				int underscore;
				if (dName.Length >= 25 && (underscore = dName.IndexOf('_')) != -1 && dName.Length > underscore + 1)
				{
					if (Util.TryParseDateTimeOffset(dName, out var d))
					{
						inProgress.Add((d, dir));
					}
				}
				else if (Util.TryParseDateTimeOffset(dName, out var d))
				{
					if (d > lastBackupTime)
					{
						lastBackupDir = dir;
						lastBackupTime = d;
					}
				}
			}
			if (inProgress.Count > 0)
			{
				if (inProgress.Count == 2)
				{
					inProgress.Sort((x, y) => x.at.CompareTo(y.at));

					lastBackupTime = inProgress[0].at;
					string lastId = GetId(inProgress[0].dir);

					now = inProgress[1].at;
					string nowId = GetId(inProgress[1].dir);
					if (nowId != lastId)
					{
						throw new InvalidDataException("The two directories in progress had differing IDs");
					}
					var lastPath = inProgress[0].dir;
					var newPath = inProgress[1].dir;
					Dir lastDir = new(lastPath, lastPath[..^lastId.Length]);
					Dir newDir = new(newPath, newPath[..^nowId.Length]);

					Console.WriteLine("Resuming interrupted job " + nowId);
					Console.WriteLine("Last Directory: " + lastDir.Name);
					Console.WriteLine("New Directory:  " + newDir.Name);

					return new PreppedDirectories(lastDir, newDir, nowId, InProgress: true);
				}
				else if (inProgress.Count == 1)
				{
					// If there's just one directory in progress, we just undo it
					string dir = inProgress[0].dir;
					string id = GetId(dir);
					if (id.Length > 0)
					{
						Directory.Move(dir, dir.Substring(0, dir.Length - id.Length));
					}

					// And then start over again
					return PrepDirectories(destination);
				}
				else
				{
					throw new InvalidDataException("Not exactly two directories in progress...");
				}
				static string GetId(string str)
				{
					int underscoreIndex = str.LastIndexOf('_');
					return underscoreIndex == -1 ? "" : str[underscoreIndex..];
				}
			}
			else
			{
				string id = "_" + Guid.NewGuid().ToString("N");
				if (lastBackupDir != null && Directory.Exists(lastBackupDir))
				{
					// We do a trick here to make the common case of "very few files modified" quick.
					// What we do is rename the last backup directory to the new backup directory, and the new one becomes the last backup directory
					// So for example...
					// Rename last to this: 2020-01-01T00-00-00+00-00 -> 2020-01-02T00-00-00+00-00_JOBID
					// Create: 2020-01-01T00-00-00+00-00_JOBID

					Dir lastDir = new(lastBackupDir + id, lastBackupDir);
					string newDirNameWithoutId = Path.Join(destination, Util.DateTimeOffsetToString(now));
					Dir newDir = new(newDirNameWithoutId + id, newDirNameWithoutId);

					// Let these exceptions throw
					Directory.Move(lastBackupDir, newDir.Name);
					Directory.CreateDirectory(lastDir.Name).CreationTimeUtc = lastBackupTime.UtcDateTime;
					Directory.SetCreationTimeUtc(newDir.Name, now.UtcDateTime);

					Console.WriteLine("Last Directory: " + lastDir.Name);
					Console.WriteLine("New Directory:  " + newDir.Name);
					return new PreppedDirectories(lastDir, newDir, id, InProgress: false);
				}
				else
				{
					string newDirNameWithoutId = Path.Join(destination, Util.DateTimeOffsetToString(now));
					Dir newDir = new(newDirNameWithoutId + id, newDirNameWithoutId);
					Directory.CreateDirectory(newDir.Name);
					Console.WriteLine("New Directory:  " + newDir.Name);
					return new PreppedDirectories(null, newDir, id, InProgress: false);
				}
			}
		}
		private static IEnumerable<Task<bool>> GetChanges(string? lastDir, string newDir, bool fatFileTimes, bool deleteFiles, IEnumerable<string> sourceFiles)
		{
			if (lastDir == null)
			{
				// All files are modified
				foreach (string srcFile in sourceFiles)
				{
					string newFile = Path.Join(newDir, srcFile);
					string lastFile = Path.Join(lastDir, srcFile);
					FileInfo sf = new(srcFile);
					FileInfo lf = new(lastFile);
					FileInfo nf = new(newFile);
					yield return HandleNewFile(sf, nf, lf);
				}
			}
			else
			{
				HashSet<string> extraFilesInNewdir = deleteFiles
					? Directory.EnumerateFiles(newDir, "*.*", SearchOption.AllDirectories)
						.Select(x => Path.GetRelativePath(newDir, x))
						.ToHashSet(StringComparer.OrdinalIgnoreCase)
					: [];
				foreach (var srcFile in sourceFiles)
				{
					extraFilesInNewdir.Remove(srcFile);
					string newFile = Path.Join(newDir, srcFile);
					string lastFile = Path.Join(lastDir, srcFile);
					FileInfo sf = new(srcFile);
					FileInfo lf = new(lastFile);
					FileInfo nf = new(newFile);

					if (sf.Exists)
					{
						if (nf.Exists)
						{
							// Source file and new exists, but last file does not
							// Check to see if the file has been modified
							// We do this regardless of whether or not the last file exists
							if (sf.Length != nf.Length
								|| (!fatFileTimes && sf.LastWriteTimeUtc != nf.LastWriteTimeUtc)
								|| (fatFileTimes && Absolute(sf.LastWriteTimeUtc - nf.LastWriteTimeUtc) > TwoSeconds))
							{
								// Modified file
								// if the last file exists, this may be because we got cut off halfway through a modified task; the file was moved, and then we were copying a file, but the drive was yanked partway through.
								// So to be safe, DON'T TOUCH the last file if it already exists!
								if (lf.Exists)
								{
									yield return HandleNewFile(sf, nf, lf);
								}
								else
								{
									yield return HandleModifiedFile(sf, nf, lf, fatFileTimes);
								}
							}
							else
							{
								// Unchanged file
							}
						}
						else
						{
							// A new file
							// Technically, if the last file exists, this could be a modified file that got moved
							// But was cut off before the source file could be copied over correctly
							// However, we still want to just copy the source file because that solves the problem
							yield return HandleNewFile(sf, nf, lf);
						}
					}
					else
					{
						if (nf.Exists)
						{
							// Deleted file
							yield return HandleDeletedFile(sf, nf, lf, fatFileTimes);
						}
					}
				}
				// Extra files should be deleted
				foreach (var extraFile in extraFilesInNewdir)
				{
					string newFile = Path.Join(newDir, extraFile);
					string lastFile = Path.Join(lastDir, extraFile);
					FileInfo lf = new(lastFile);
					FileInfo nf = new(newFile);
					yield return HandleDeletedFile(null, nf, lf, fatFileTimes);
				}
			}
		}
		private static async Task<int> RunBackup(BackupArgs parsedArgs)
		{
			PreppedDirectories dirs;
			try
			{
				dirs = PrepDirectories(parsedArgs.Repository);
			}
			catch (Exception ex)
			{
				await Console.Out.WriteLineAsync(ex.ToString());
				return ExitCode.ErrorPreppingDirectories;
			}

			string allText;
			try
			{
				allText = File.ReadAllText(parsedArgs.ReadFilesFrom);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				return ExitCode.ErrorReadingFileList;
			}

			string[] allFilesToCheck = allText.Split('\0', StringSplitOptions.RemoveEmptyEntries);
			List<Task<bool>> allTasks = new(allFilesToCheck.Length);
			foreach (var t in GetChanges(dirs.LastDir?.Name, dirs.NewDir.Name, parsedArgs.FatFileTimes, parsedArgs.DeleteFiles, allFilesToCheck))
			{
				allTasks.Add(t);
			}
			bool[] bs = await Task.WhenAll(allTasks);
			if (!bs.All(x => x))
			{

			}
			if (dirs.LastDir != null && Directory.Exists(dirs.LastDir.Name))
			{
				await Util.Try(5,
					string.Concat("Renaming directory \"", dirs.LastDir.Name, "\" to \"", dirs.LastDir.FinishName, "\""),
					string.Concat("Failed to rename directory \"", dirs.LastDir.Name, "\" to \"", dirs.LastDir.FinishName, "\" because "),
					() => Directory.Move(dirs.LastDir.Name, dirs.LastDir.FinishName));
			}
			await Util.Try(5,
					string.Concat("Renaming directory \"", dirs.NewDir.Name, "\" to \"", dirs.NewDir.FinishName, "\""),
					string.Concat("Failed to rename directory \"", dirs.NewDir.Name, "\" to \"", dirs.NewDir.FinishName, "\" because "),
					() => Directory.Move(dirs.NewDir.Name, dirs.NewDir.FinishName));

			return ExitCode.Ok;
		}
		public static async Task<bool> HandleNewFile(FileInfo srcFile, FileInfo newFile, FileInfo lastFile)
		{
			if (srcFile.Exists)
			{
				if (newFile.DirectoryName != null && !Directory.Exists(newFile.DirectoryName))
				{
					bool b1 = await Util.Try(5, "Creating directory " + newFile.DirectoryName, string.Concat("Failed to create directory \"", newFile.DirectoryName, "\" because"), () => Directory.CreateDirectory(newFile.DirectoryName));
					if (!b1) return false;
				}
				bool b2 = await Util.Try(5, "Copying " + srcFile.FullName, string.Concat("Failed to copy \"", srcFile.FullName, "\" because "), async () =>
				{
					using FileStream src = new(srcFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
					using (FileStream dst = new(newFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read))
					{
						await src.CopyToAsync(dst);
					}
					newFile.LastWriteTimeUtc = srcFile.LastWriteTimeUtc;
					newFile.CreationTimeUtc = srcFile.CreationTimeUtc;
				});
				return b2;
			}
			return true;
		}
		public static async Task<bool> HandleDeletedFile(FileInfo? srcFile, FileInfo newFile, FileInfo lastFile, bool fatFileTimes)
		{
			if (newFile.Exists)
			{
				if (!lastFile.Exists)
				{
					if (lastFile.DirectoryName != null && !Directory.Exists(lastFile.DirectoryName))
					{
						bool b1 = await Util.Try(5, "Creating directory " + lastFile.DirectoryName, string.Concat("Failed to create directory \"", lastFile.DirectoryName, "\" because"), () => Directory.CreateDirectory(lastFile.DirectoryName));
						if (!b1) return false;
					}

					return await Util.Try(5, "Moving " + newFile.FullName, string.Concat("Failed to move \"", newFile.FullName, "\" because "), () => newFile.MoveTo(lastFile.FullName));
				}
				else
				{
					if (newFile.Length != lastFile.Length
						|| (!fatFileTimes && newFile.LastWriteTimeUtc != lastFile.LastWriteTimeUtc)
						|| (fatFileTimes && Absolute(newFile.LastWriteTimeUtc - lastFile.LastWriteTimeUtc) > TwoSeconds))
					{
						// Modified file; we need to overwrite the last file with the new file
						bool b1 = await Util.Try(5, "Deleting " + lastFile.FullName, string.Concat("Failed to delete \"", lastFile.FullName, "\" because "), lastFile.Delete);
						if (!b1) return false;
						return await Util.Try(5, "Moving " + newFile.FullName, string.Concat("Failed to move \"", newFile.FullName, "\" because "), () => newFile.MoveTo(lastFile.FullName));
					}
					else
					{
						// Unchanged file; just delete the new file
						return await Util.Try(5, "Deleting " + newFile.FullName, string.Concat("Failed to delete \"", newFile.FullName, "\" because "), newFile.Delete);
					}
				}
			}
			return true;
		}
		public static async Task<bool> HandleModifiedFile(FileInfo srcFile, FileInfo newFile, FileInfo lastFile, bool fatFileTimes)
		{
			if (!await HandleDeletedFile(srcFile, newFile, lastFile, fatFileTimes)) return false;
			if (!await HandleNewFile(srcFile, newFile, lastFile)) return false;
			return true;
		}
		public static TimeSpan Absolute(TimeSpan ts)
		{
			return ts < TimeSpan.Zero ? -ts : ts;
		}
	}
}
