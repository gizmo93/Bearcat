namespace Bearcat.Domain.UseCases.ReplaceHosters.Dto;

public sealed record HosterReplacementAppliedChangeCounts(
    int MovedUploadCount,
    int DeactivatedAccountCount,
    int UploadCountMarkedOffline
);
