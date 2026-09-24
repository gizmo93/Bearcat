using Bearcat.Domain.Entities;

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

    public int Priority { get; set; } = RemoteSourceAutomation.DefaultPriority;

    public bool IgnoreExistingOnFirstScan { get; set; } = true;
}
