using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.UseCases.ManageUploads.Exceptions;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadCreation;

public class UploadCreationService(
    IUploadStateRepository uploadStateRepository,
    TimeProvider timeProvider,
    IApplicationConfigurationProvider configuration,
    INotificationService notificationService,
    ILogger<UploadCreationService> logger,
    QualityGateEvaluator qualityGateEvaluator
)
{
    private const int MaximumAutomaticReuploadsPerRun = 100;

    public async Task CreateMissingInitialUploadsAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var cooldownMinutes = Math.Max(
            0,
            configuration.GetValue<InitialUploadConfiguration>(c => c.CooldownMinutes)
        );
        var releaseCreatedBefore = localNow.AddMinutes(-cooldownMinutes);
        var uploadConfigsWithoutUploads =
            await uploadStateRepository.GetUploadConfigsWithoutUploadsAsync(
                releaseCreatedBefore,
                cancellationToken
            );

        foreach (var releaseUploadConfigs in uploadConfigsWithoutUploads.GroupBy(uc => uc.Release))
        {
            CreateInitialUploadsForReleaseIfQualityGateIsSatisfied(
                releaseUploadConfigs.Key,
                releaseUploadConfigs.ToList(),
                localNow
            );
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<InitialUploadCreationForReleaseResult> CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigsWithoutUploads =
            await uploadStateRepository.GetUploadConfigsWithoutUploadsOfReleaseAsync(
                releaseId,
                cancellationToken
            );

        if (uploadConfigsWithoutUploads.Count == 0)
        {
            return new InitialUploadCreationForReleaseResult(
                CreatedUploadCount: 0,
                IsBlockedByQualityGate: false
            );
        }

        var result = CreateInitialUploadsForReleaseIfQualityGateIsSatisfied(
            uploadConfigsWithoutUploads.First().Release,
            uploadConfigsWithoutUploads,
            timeProvider.GetLocalNow()
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var uploads = await uploadStateRepository.GetUploadsDueForAutomaticReuploadAsync(
            localNow: localNow,
            maximumUploadCount: MaximumAutomaticReuploadsPerRun,
            cancellationToken: cancellationToken
        );

        var releaseIdsWithAppliedQualityGate = new HashSet<int>();

        foreach (var upload in uploads)
        {
            var release = upload.UploadConfig.Release;

            if (
                !ApplyQualityGateOnceAndCheckIfSatisfied(
                    release,
                    localNow,
                    releaseIdsWithAppliedQualityGate
                )
            )
            {
                logger.LogInformation(
                    "Skipping automatic reupload for Release {ReleaseId} because the quality gate is not satisfied",
                    release.Id
                );
                continue;
            }

            AddReuploadOfUpload(upload);

            notificationService.Create(
                kind: NotificationKind.AutomaticReuploadCreated,
                message: "Automatic reupload scheduled due to offline files",
                entity: upload,
                selector: u => u.Upload
            );
        }

        await uploadStateRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CreateManualReuploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetUploadForReuploadAsync(
            uploadId,
            cancellationToken
        );

        if (!upload.CanBeManuallyReuploaded())
        {
            throw new InvalidUploadStateException(
                "Manual reuploads can only be created for offline, partially online, canceled, or failed uploads."
            );
        }

        if (upload.HasExistingOrPendingReplacementUpload())
        {
            throw new InvalidUploadStateException(
                "A replacement upload already exists or is pending for this upload config."
            );
        }

        var newUpload = AddReuploadOfUpload(upload);

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return newUpload.Id;
    }

    private InitialUploadCreationForReleaseResult CreateInitialUploadsForReleaseIfQualityGateIsSatisfied(
        Release release,
        IReadOnlyList<UploadConfig> uploadConfigsWithoutUploads,
        DateTime localNow
    )
    {
        if (!ApplyQualityGateAndCheckIfSatisfied(release, localNow))
        {
            logger.LogInformation(
                "Skipping upload creation for Release {ReleaseId} because the quality gate is not satisfied",
                release.Id
            );
            return new InitialUploadCreationForReleaseResult(
                CreatedUploadCount: 0,
                IsBlockedByQualityGate: true
            );
        }

        foreach (var uploadConfig in uploadConfigsWithoutUploads)
        {
            var upload = AddInitialUploadForUploadConfig(uploadConfig);

            notificationService.Create(
                kind: NotificationKind.InitialUploadCreated,
                message: "Initial upload created for release",
                entity: upload,
                selector: u => u.Upload
            );

            logger.LogInformation(
                "Created missing upload for UploadConfig {UploadConfigId}",
                uploadConfig.Id
            );
        }

        return new InitialUploadCreationForReleaseResult(
            CreatedUploadCount: uploadConfigsWithoutUploads.Count,
            IsBlockedByQualityGate: false
        );
    }

    private Upload AddInitialUploadForUploadConfig(UploadConfig uploadConfig)
    {
        return AddUploadWaitingForArchive(uploadConfig, uploadConfig.HosterRegistrationId);
    }

    private Upload AddReuploadOfUpload(Upload previousUpload)
    {
        return AddUploadWaitingForArchive(
            previousUpload.UploadConfig,
            GetHosterRegistrationIdForReupload(previousUpload)
        );
    }

    private Upload AddUploadWaitingForArchive(UploadConfig uploadConfig, int? hosterRegistrationId)
    {
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            HosterRegistrationId = hosterRegistrationId,
            CreatedAt = timeProvider.GetLocalNow(),
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
        };

        uploadStateRepository.Add(upload);
        return upload;
    }

    private static int? GetHosterRegistrationIdForReupload(Upload previousUpload)
    {
        var uploadConfig = previousUpload.UploadConfig;

        if (uploadConfig.HosterAccountPoolId is null)
        {
            return uploadConfig.HosterRegistrationId;
        }

        var previousRegistration = previousUpload.HosterRegistration;

        var previousRegistrationIsStillUsable =
            previousRegistration is not null
            && previousRegistration.IsUsableForUploads()
            && uploadConfig.HosterAccountPool!.Members.Any(member =>
                member.HosterRegistrationId == previousRegistration.Id
            );

        return previousRegistrationIsStillUsable ? previousRegistration!.Id : null;
    }

    private bool ApplyQualityGateOnceAndCheckIfSatisfied(
        Release release,
        DateTime localNow,
        HashSet<int> releaseIdsWithAppliedQualityGate
    )
    {
        if (!releaseIdsWithAppliedQualityGate.Add(release.Id))
        {
            return IsQualityGateSatisfied(release);
        }

        return ApplyQualityGateAndCheckIfSatisfied(release, localNow);
    }

    private bool ApplyQualityGateAndCheckIfSatisfied(Release release, DateTime localNow)
    {
        if (release.QualityGateState is QualityGateState.NotEvaluated or QualityGateState.Passed)
        {
            qualityGateEvaluator.EvaluateAndApply(release, localNow);
        }

        return IsQualityGateSatisfied(release);
    }

    private static bool IsQualityGateSatisfied(Release release)
    {
        return release.QualityGateState
            is QualityGateState.Passed
                or QualityGateState.ManuallyApproved;
    }
}
