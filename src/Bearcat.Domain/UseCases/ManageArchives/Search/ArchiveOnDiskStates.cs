using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.Search;

public static class ArchiveOnDiskStates
{
    public static IReadOnlyList<ArchiveState> OnDisk { get; } = [ArchiveState.Created];

    public static IReadOnlyList<ArchiveState> NotOnDisk { get; } =
    [ArchiveState.Deleted, ArchiveState.MissingFiles, ArchiveState.CreationFailed];

    public static IReadOnlyList<ArchiveState> GetArchiveStates(ArchiveOnDiskFilter filter) =>
        filter switch
        {
            ArchiveOnDiskFilter.OnDisk => OnDisk,
            ArchiveOnDiskFilter.NotOnDisk => NotOnDisk,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
        };
}
