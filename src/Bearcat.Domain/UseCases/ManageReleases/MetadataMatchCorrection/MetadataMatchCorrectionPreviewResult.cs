namespace Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;

public record MetadataMatchCorrectionPreviewResult(
    MetadataMatchCorrectionFailure? Failure,
    MetadataMatchCorrectionPreview? Preview
);
