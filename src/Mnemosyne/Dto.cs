namespace Mnemosyne
{
	public sealed record class Dir(string Name, string FinishName);
	public sealed record class PreppedDirectories(Dir? LastDir, Dir NewDir, string Id, bool InProgress);
}
