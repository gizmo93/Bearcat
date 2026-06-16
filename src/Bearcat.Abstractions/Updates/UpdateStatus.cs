namespace Bearcat.Abstractions.Updates;

public sealed record UpdateStatus(
    string CurrentVersion,
    string? LatestVersion,
    bool IsUpdateAvailable,
    string ReleaseUrl
);
