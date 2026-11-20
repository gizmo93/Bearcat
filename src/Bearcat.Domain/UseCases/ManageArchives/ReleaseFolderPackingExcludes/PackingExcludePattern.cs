using System.IO.Enumeration;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPackingExcludes;

public record PackingExcludePattern(string NamePattern, bool AppliesToFolders)
{
    private const char FolderPatternSuffix = '/';

    public static List<PackingExcludePattern> ParsePatterns(string commaSeparatedPatterns)
    {
        return commaSeparatedPatterns
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParsePattern)
            .ToList();
    }

    public bool Matches(string name)
    {
        return FileSystemName.MatchesSimpleExpression(NamePattern, name, ignoreCase: true);
    }

    private static PackingExcludePattern ParsePattern(string pattern)
    {
        return pattern.EndsWith(FolderPatternSuffix)
            ? new PackingExcludePattern(
                pattern.TrimEnd(FolderPatternSuffix),
                AppliesToFolders: true
            )
            : new PackingExcludePattern(pattern, AppliesToFolders: false);
    }
}
