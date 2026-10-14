namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFileHashes;

public class KnownArchiveFileHashes(IReadOnlyList<string> knownHashes)
{
    private readonly HashSet<string> hashes = knownHashes.ToHashSet(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly Lock knownHashesLock = new();

    public bool AddIfUnknown(string md5Hash)
    {
        lock (knownHashesLock)
        {
            return hashes.Add(md5Hash);
        }
    }
}
