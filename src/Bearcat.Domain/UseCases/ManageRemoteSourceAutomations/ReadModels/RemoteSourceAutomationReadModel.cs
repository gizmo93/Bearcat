using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.ReadModels;

public record RemoteSourceAutomationReadModel(
    int Id,
    string Name,
    int RemoteSourceRegistrationId,
    string RemoteSourceRegistrationName,
    bool IsRemoteSourceRegistrationActive,
    string RemotePath,
    string TargetPath,
    string? FolderNamePattern,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    ReleaseType ReleaseType,
    string? PrimaryLanguageCode,
    bool KeepRawFiles,
    int Priority,
    bool IsEnabled,
    bool IgnoreExistingOnFirstScan,
    IReadOnlyDictionary<RemoteSourceDownloadState, int> DownloadCountsByState
);
