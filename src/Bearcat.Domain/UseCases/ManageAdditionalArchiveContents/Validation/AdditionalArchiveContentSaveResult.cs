namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Validation;

public record AdditionalArchiveContentSaveResult(
    int? AdditionalArchiveContentId,
    IReadOnlyList<AdditionalArchiveContentValidationError> ValidationErrors
)
{
    public bool IsSuccess => ValidationErrors.Count == 0;

    public static AdditionalArchiveContentSaveResult Saved(int additionalArchiveContentId)
    {
        return new AdditionalArchiveContentSaveResult(additionalArchiveContentId, []);
    }

    public static AdditionalArchiveContentSaveResult Invalid(
        IReadOnlyList<AdditionalArchiveContentValidationError> validationErrors
    )
    {
        return new AdditionalArchiveContentSaveResult(null, validationErrors);
    }
}
