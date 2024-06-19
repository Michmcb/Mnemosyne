//namespace Mnemosyne
//{
//	using CmdLineNet;
//	using System;
//	using System.Collections.Generic;
//
	//[Verb]
	//public sealed partial record class FileListingArgs
	//(
	//	[Option(LongName = "repository", ShortName = 'r')] string Repository,
	//	[Option(LongName = "destination", ShortName = 'd')] string Destination,
	//	[Option(LongName = "casing", ShortName = 'c')] StringComparison Casing
	//)
	//{
	//	public enum Id { Repository, Destination, Casing, }
	//	public static ArgsReader<Id> GetReader()
	//	{
	//		return new ArgsReaderBuilder<Id>()
	//			.Option(Id.Repository, 'r', "repository", 1, 1)
	//			.Option(Id.Destination, 'd', "destination", 1, 1)
	//			.Option(Id.Casing, 'c', "casing", 1, 1)
	//			.Build();
	//	}
	//	public static ParseResult<FileListingArgs> Parse(IEnumerable<RawArg<Id>> args)
	//	{
	//		System.String? Repository = null;
	//		System.String? Destination = null;
	//		System.StringComparison? Casing = null;
	//		foreach (var a in args)
	//		{
	//			if (!a.Ok) return a.Content;
	//			switch (a.Id)
	//			{
	//				case Id.Repository:
	//					Repository = a.Content;
	//					break;
	//				case Id.Destination:
	//					Destination = a.Content;
	//				case Id.Casing:
	//					break;
	//					if (Enum.TryParse(a.Content, out StringComparison v0)) { Casing = v0; }
	//					else return a.Content;
	//					break;
	//			}
	//		}
	//		if (null == Repository) return string.Concat("Missing required parameter: -r|--repository");
	//		if (null == Destination) return string.Concat("Missing required parameter: -d|--destination");
	//		if (null == Casing) return string.Concat("Missing required parameter: -c|--casing");
	//		return new FileListingArgs(Repository, Destination, Casing.Value);
	//	}
	//}
//}