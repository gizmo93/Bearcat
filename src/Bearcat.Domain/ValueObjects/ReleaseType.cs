namespace Bearcat.Domain.ValueObjects;

public enum ReleaseType
{
    Managed = 1,
    Unmanaged = 2,
    Remote = 3,
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
                ReleaseType.Remote =>
                    "Neither release data nor archives are stored locally. Bearcat downloads archives from an online mirror when a reupload is needed",
                _ => throw new ArgumentOutOfRangeException(nameof(releaseType), releaseType, null),
            };
    }
}
