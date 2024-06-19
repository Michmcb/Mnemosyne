namespace Mnemosyne
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	public sealed record class Verb(Func<IEnumerable<string>, Task<int>> Execute, Action WriteHelp, string Description);
}
