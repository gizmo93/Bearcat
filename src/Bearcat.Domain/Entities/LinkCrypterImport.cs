using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class LinkCrypterImport
{
    public int Id { get; set; }

    public int LinkCrypterRegistrationId { get; set; }

    public LinkCrypterRegistration LinkCrypterRegistration { get; set; } = null!;

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public string? ArchiveFilesBasePathForReleasesWithoutLocalFiles { get; set; }

    public string? PrimaryLanguageCode { get; set; }

    public LinkCrypterImportState State { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int ProcessedContainerCount { get; set; }

    public int ContainerCountToProcess { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public List<LinkCrypterImportHosterRegistration> HosterRegistrations { get; set; } = [];

    public List<LinkCrypterImportContainer> Containers { get; set; } = [];

    public void StartProgress(int containerCountToProcess)
    {
        ProcessedContainerCount = 0;
        ContainerCountToProcess = containerCountToProcess;
    }

    public void ChangeState(LinkCrypterImportState state, DateTime localNow)
    {
        State = state;
        UpdatedAt = localNow;
    }

    public void MarkAsFailed(IReadOnlyList<string> errorMessages, DateTime localNow)
    {
        ChangeState(LinkCrypterImportState.Failed, localNow);
        ErrorMessages = errorMessages.ToList();
    }
}
