namespace Bearcat.Domain.Entities;

public class UploadCompletionActionExecution
{
    public int Id { get; set; }

    public int? ReleaseId { get; set; }

    public Release? Release { get; set; }

    public int? ReleaseCollectionId { get; set; }

    public ReleaseCollection? ReleaseCollection { get; set; }

    public int UploadCompletionActionRegistrationId { get; set; }

    public UploadCompletionActionRegistration UploadCompletionActionRegistration { get; set; } =
        null!;

    public DateTime HandledUploadedAt { get; set; }

    public DateTime ExecutedAt { get; set; }

    public string? ResultText { get; set; }

    public string? ErrorMessage { get; set; }
}
