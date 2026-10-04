namespace Bearcat.Website.Pages.ManageReleases;

public static class DatabaseDisplayName
{
    public static string GetFromClassName(string className)
    {
        if (className.Equals("XrelNfoDatabase", StringComparison.OrdinalIgnoreCase))
        {
            return "xREL";
        }

        const string nfoSuffix = "NfoDatabase";
        if (className.EndsWith(nfoSuffix, StringComparison.Ordinal))
        {
            return className[..^nfoSuffix.Length];
        }

        const string metadataSuffix = "MetadataDatabase";
        if (className.EndsWith(metadataSuffix, StringComparison.Ordinal))
        {
            return className[..^metadataSuffix.Length];
        }

        return className;
    }
}
