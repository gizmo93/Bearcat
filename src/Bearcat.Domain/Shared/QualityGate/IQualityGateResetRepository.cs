namespace Bearcat.Domain.Shared.QualityGate;

public interface IQualityGateResetRepository
{
    Task ResetForQualityProfileAsync(int qualityProfileId, CancellationToken cancellationToken);

    Task ResetForReleaseGroupAsync(int releaseGroupId, CancellationToken cancellationToken);
}
