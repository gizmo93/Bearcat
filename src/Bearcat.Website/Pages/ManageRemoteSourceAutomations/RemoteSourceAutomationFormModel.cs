using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageRemoteSourceAutomations;

public class RemoteSourceAutomationFormModel
{
    public int? RemoteSourceAutomationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? RemoteSourceRegistrationId { get; set; }

    public string RemotePath { get; set; } = "/";

    public string TargetPath { get; set; } = string.Empty;

    public string? FolderNamePattern { get; set; }

    public int? ReleaseTemplateId { get; set; }

    public string PrimaryLanguageCode { get; set; } = string.Empty;

    public bool KeepRawFiles { get; set; } = true;

    public bool ExtractArchivesBeforeReleaseCreation { get; set; }

    public int Priority { get; set; } = RemoteSourceAutomation.DefaultPriority;

    public bool IgnoreExistingOnFirstScan { get; set; } = true;

    public RemoteSourceAutomationInput CreateInput(ReleaseType? selectedReleaseType)
    {
        var isManagedTemplate = selectedReleaseType is ReleaseType.Managed;

        return new RemoteSourceAutomationInput
        {
            Name = Name,
            RemoteSourceRegistrationId = RemoteSourceRegistrationId!.Value,
            RemotePath = RemotePath,
            TargetPath = TargetPath,
            FolderNamePattern = FolderNamePattern,
            ReleaseTemplateId = ReleaseTemplateId!.Value,
            PrimaryLanguageCode = PrimaryLanguageCode,
            KeepRawFiles = !isManagedTemplate || KeepRawFiles,
            ExtractArchivesBeforeReleaseCreation =
                isManagedTemplate && ExtractArchivesBeforeReleaseCreation,
            Priority = Priority,
            IgnoreExistingOnFirstScan = IgnoreExistingOnFirstScan,
        };
    }
}
