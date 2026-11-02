using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class LinkCrypterImportContainer
{
    public int Id { get; set; }

    public int LinkCrypterImportId { get; set; }

    public LinkCrypterImport LinkCrypterImport { get; set; } = null!;

    public string ContainerId { get; set; } = null!;

    public string ContainerUrl { get; set; } = null!;

    public string ContainerName { get; set; } = null!;

    public string ReleaseName { get; set; } = null!;

    public string ExternalReference { get; set; } = null!;

    public bool HasBackupLinks { get; set; }

    public bool IsSelected { get; set; }

    public LinkCrypterImportContainerStatus Status { get; set; }

    public LinkCrypterImportNotImportableReason? NotImportableReason { get; set; }

    public int? ImportedReleaseId { get; set; }

    public Release? ImportedRelease { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public string PreviewJson { get; set; } = null!;
}
