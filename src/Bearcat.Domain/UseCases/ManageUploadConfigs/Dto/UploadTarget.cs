namespace Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

public sealed record UploadTarget
{
    private UploadTarget(int? hosterRegistrationId, int? hosterAccountPoolId)
    {
        HosterRegistrationId = hosterRegistrationId;
        HosterAccountPoolId = hosterAccountPoolId;
    }

    public int? HosterRegistrationId { get; }

    public int? HosterAccountPoolId { get; }

    public static UploadTarget ForHosterRegistration(int hosterRegistrationId) =>
        new(hosterRegistrationId, null);

    public static UploadTarget ForHosterAccountPool(int hosterAccountPoolId) =>
        new(null, hosterAccountPoolId);

    public static UploadTarget FromIds(int? hosterRegistrationId, int? hosterAccountPoolId) =>
        hosterAccountPoolId is { } existingHosterAccountPoolId
            ? ForHosterAccountPool(existingHosterAccountPoolId)
            : ForHosterRegistration(hosterRegistrationId!.Value);
}
