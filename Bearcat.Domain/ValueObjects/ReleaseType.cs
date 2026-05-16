namespace Bearcat.Domain.ValueObjects;

public enum ReleaseType
{
    Managed = 1,
    Unmanaged = 2,
}

public static class ReleaseTypeExtensions
{
    extension(ReleaseType releaseType)
    {
        public string Description =>
            releaseType switch
            {
                ReleaseType.Managed =>
                    "Bearcat creates the archives and manages uploads automatically",
                ReleaseType.Unmanaged => "You create the archives and Bearcat just uploads them",
                _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null),
            };
    }
}
