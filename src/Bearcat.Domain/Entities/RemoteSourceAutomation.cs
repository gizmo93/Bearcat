namespace Bearcat.Domain.Entities;

public class RemoteSourceAutomation
{
    public const int DefaultPriority = 100;

    public int Id { get; set; }

    public required string Name { get; set; }

    public int RemoteSourceRegistrationId { get; set; }

    public RemoteSourceRegistration RemoteSourceRegistration { get; set; } = null!;

    public required string RemotePath { get; set; }

    public required string TargetPath { get; set; }

    public string? FolderNamePattern { get; set; }

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public string? PrimaryLanguageCode { get; set; }

    public bool KeepRawFiles { get; set; } = true;

    public int Priority { get; set; } = DefaultPriority;

    public bool IsEnabled { get; set; }

    public bool IgnoreExistingOnFirstScan { get; set; }

    public bool HasCompletedInitialScan { get; set; }
}
