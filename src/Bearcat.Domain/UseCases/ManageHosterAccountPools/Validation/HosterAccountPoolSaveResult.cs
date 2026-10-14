namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Validation;

public record HosterAccountPoolSaveResult(
    int? HosterAccountPoolId,
    IReadOnlyList<HosterAccountPoolValidationError> ValidationErrors
)
{
    public bool IsSuccess => ValidationErrors.Count == 0;

    public static HosterAccountPoolSaveResult Saved(int hosterAccountPoolId)
    {
        return new HosterAccountPoolSaveResult(hosterAccountPoolId, []);
    }

    public static HosterAccountPoolSaveResult Invalid(
        IReadOnlyList<HosterAccountPoolValidationError> validationErrors
    )
    {
        return new HosterAccountPoolSaveResult(null, validationErrors);
    }
}
