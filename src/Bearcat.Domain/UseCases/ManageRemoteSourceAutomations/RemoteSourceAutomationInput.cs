using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;

public sealed record RemoteSourceAutomationInput
{
    public required string Name { get; init; }

    public required int RemoteSourceRegistrationId { get; init; }

    public required string RemotePath { get; init; }

    public required string TargetPath { get; init; }

    public string? FolderNamePattern { get; init; }

    public required int ReleaseTemplateId { get; init; }

    public string? PrimaryLanguageCode { get; init; }

    public bool KeepRawFiles { get; init; } = true;

    public int Priority { get; init; } = RemoteSourceAutomation.DefaultPriority;

    public bool IgnoreExistingOnFirstScan { get; init; }
}
