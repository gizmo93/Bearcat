using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class RemoteSourceDownload
{
    public int Id { get; set; }

    public int? RemoteSourceAutomationId { get; set; }

    public RemoteSourceAutomation? RemoteSourceAutomation { get; set; }

    public int? RemoteSourceRegistrationId { get; set; }

    public RemoteSourceRegistration? RemoteSourceRegistration { get; set; }

    public required string SourceName { get; set; }

    public required string RemoteFolderPath { get; set; }

    public required string FolderName { get; set; }

    public required string LocalFolderPath { get; set; }

    public int? ReleaseTemplateId { get; set; }

    public ReleaseTemplate? ReleaseTemplate { get; set; }

    public string? PrimaryLanguageCode { get; set; }

    public bool KeepRawFiles { get; set; }

    public RemoteSourceDownloadState State { get; set; }

    public int FileCount { get; set; }

    public long TotalBytes { get; set; }

    public DateTime LastChangedAt { get; set; }

    public DateTime DiscoveredAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int? ReleaseId { get; set; }

    public Release? Release { get; set; }
}
