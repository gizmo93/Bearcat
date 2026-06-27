namespace Bearcat.Website;

public sealed class WorkingDirectoriesConfig
{
    public string[]? WorkingDirectories { get; set; }

    public string? ReleaseDataDirectory { get; set; }

    public IReadOnlyList<string> GetWorkingDirectories()
    {
        return WorkingDirectories ?? [ReleaseDataDirectory ?? string.Empty];
    }
}
