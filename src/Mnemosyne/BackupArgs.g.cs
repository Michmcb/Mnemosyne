#nullable enable
namespace Mnemosyne
{
	using System;
	using System.Collections.Generic;
	using CmdLineNet;
	public sealed partial record class BackupArgs(string ReadFilesFrom, string Repository, bool FatFileTimes, bool DeleteFiles) : ICmdParseable<BackupArgs.Id, BackupArgs>
	{
		public enum Id{ReadFilesFrom,Repository,FatFileTimes,DeleteFiles,}
		private static ArgsReader<Id> _reader = new ArgsReaderBuilder<Id>()
			.Option(Id.ReadFilesFrom, "read-files-from", 1, 1, "A text file from which files to be backed up are read. Individual files must be separated with the null character. Paths are relative to the current directory.")
			.Option(Id.Repository, 'r', "repository", 1, 1, "The base directory where all backup directories are stored.")
			.Switch(Id.FatFileTimes, 'f', "fat-file-times", 1, 1, "Assumes the directory indicated by Repository is on a volume which only supports filetimes up to a resolution of 2 seconds. File time differences of up to 2 seconds are not considered modified.")
			.Switch(Id.DeleteFiles, 'd', "delete-files", 1, 1, "When files are no longer in ReadFilesFrom, then they are deleted from the latest directory in Repository.")
			.Build();
		public static ArgsReader<Id> GetReader()
		{
			return _reader;
		}
		public static ParseResult<BackupArgs> Parse(IEnumerable<RawArg<Id>> args)
		{
			string? ReadFilesFrom = null;
			string? Repository = null;
			bool FatFileTimes = false;
			bool DeleteFiles = false;
			foreach (var a in args)
			{
				if (!a.Ok) return a.Content;
				switch (a.Id)
				{
					case Id.ReadFilesFrom:
						ReadFilesFrom = a.Content;
						break;
					case Id.Repository:
						Repository = a.Content;
						break;
					case Id.FatFileTimes:
						FatFileTimes = true;
						break;
					case Id.DeleteFiles:
						DeleteFiles = true;
						break;
				}
			}
			if (null == ReadFilesFrom) return "Missing required option: --read-files-from";
			if (null == Repository) return "Missing required option: -r|--repository";
			return new BackupArgs(ReadFilesFrom, Repository, FatFileTimes, DeleteFiles);
		}
	}
}
#nullable restore