using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageProxyServers.Selection;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageHosters;

public class HosterRegistrationService(
    IHosterConfigurationWriteRepository writeRepository,
    IHosterConfigurationReadRepository readRepository,
    IHosterFactory hosterFactory,
    HosterCaptchaVerificationService captchaVerificationService,
    ISecretProtector secretProtector,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService,
    ProxySelectionValidator proxySelectionValidator
)
{
    public async Task<int> RegisterHosterAsync(
        string name,
        bool isActive,
        Dictionary<string, string> configuration,
        string hosterClassName,
        int? maxParallelUploadsOverride = null,
        int? numberOfHoursUntilReuploadOverride = null,
        ReuploadTrigger? reuploadTriggerOverride = null,
        bool alwaysReuploadAllFiles = false,
        bool useForMirrorDownloads = false,
        int mirrorPriority = 100,
        ProxySelection uploadProxySelection = ProxySelection.UseCategoryDefault,
        int? uploadProxyServerId = null,
        ProxySelection mirrorDownloadProxySelection = ProxySelection.UseCategoryDefault,
        int? mirrorDownloadProxyServerId = null,
        CancellationToken cancellationToken = default
    )
    {
        var hoster = hosterFactory.GetByName(hosterClassName);
        var serializedConfig = hoster.SerializeHosterConfig(configuration);
        var effectiveMirrorDownloadProxySelection = GetEffectiveMirrorDownloadProxySelection(
            hoster,
            mirrorDownloadProxySelection
        );

        var registration = new HosterRegistration
        {
            Name = name,
            IsActive = isActive,
            SerializedConfig = secretProtector.Protect(serializedConfig),
            HosterClassName = hosterClassName,
            MaxParallelUploadsOverride = hoster.HasFixedParallelUploadLimit
                ? null
                : maxParallelUploadsOverride,
            NumberOfHoursUntilReuploadOverride = numberOfHoursUntilReuploadOverride,
            ReuploadTriggerOverride = reuploadTriggerOverride,
            AlwaysReuploadAllFiles = alwaysReuploadAllFiles,
            UseForMirrorDownloads = useForMirrorDownloads && hoster is IHosterWithDownload,
            MirrorPriority = mirrorPriority,
            UploadProxySelection = uploadProxySelection,
            UploadProxyServerId = await proxySelectionValidator.GetProxyServerIdToStoreAsync(
                uploadProxySelection,
                uploadProxyServerId,
                cancellationToken
            ),
            MirrorDownloadProxySelection = effectiveMirrorDownloadProxySelection,
            MirrorDownloadProxyServerId =
                await proxySelectionValidator.GetProxyServerIdToStoreAsync(
                    effectiveMirrorDownloadProxySelection,
                    mirrorDownloadProxyServerId,
                    cancellationToken
                ),
        };

        writeRepository.Add(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
        return registration.Id;
    }

    public async Task<int> GetUploadCountAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await readRepository.GetUploadCountAsync(id, cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        writeRepository.Remove(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRegistrationAsync(
        int id,
        string name,
        Dictionary<string, string> configuration,
        int? maxParallelUploadsOverride = null,
        int? numberOfHoursUntilReuploadOverride = null,
        ReuploadTrigger? reuploadTriggerOverride = null,
        bool alwaysReuploadAllFiles = false,
        bool useForMirrorDownloads = false,
        int mirrorPriority = 100,
        ProxySelection uploadProxySelection = ProxySelection.UseCategoryDefault,
        int? uploadProxyServerId = null,
        ProxySelection mirrorDownloadProxySelection = ProxySelection.UseCategoryDefault,
        int? mirrorDownloadProxyServerId = null,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var hoster = hosterFactory.GetByName(registration.HosterClassName);
        var effectiveMirrorDownloadProxySelection = GetEffectiveMirrorDownloadProxySelection(
            hoster,
            mirrorDownloadProxySelection
        );
        var mergedConfiguration = StoredConfigurationMerger.MergeSubmittedIntoStoredConfiguration(
            registration: registration,
            submittedConfiguration: configuration,
            deserializeStoredConfiguration: serializedConfig =>
                hoster.DeserializeHosterConfig(serializedConfig).ToDictionary(),
            secretProtector: secretProtector
        );

        registration.Name = name;
        registration.MaxParallelUploadsOverride = hoster.HasFixedParallelUploadLimit
            ? null
            : maxParallelUploadsOverride;
        registration.NumberOfHoursUntilReuploadOverride = numberOfHoursUntilReuploadOverride;
        registration.ReuploadTriggerOverride = reuploadTriggerOverride;
        registration.AlwaysReuploadAllFiles = alwaysReuploadAllFiles;
        registration.UseForMirrorDownloads = useForMirrorDownloads && hoster is IHosterWithDownload;
        registration.MirrorPriority = mirrorPriority;
        registration.UploadProxySelection = uploadProxySelection;
        registration.UploadProxyServerId =
            await proxySelectionValidator.GetProxyServerIdToStoreAsync(
                uploadProxySelection,
                uploadProxyServerId,
                cancellationToken
            );
        registration.MirrorDownloadProxySelection = effectiveMirrorDownloadProxySelection;
        registration.MirrorDownloadProxyServerId =
            await proxySelectionValidator.GetProxyServerIdToStoreAsync(
                effectiveMirrorDownloadProxySelection,
                mirrorDownloadProxyServerId,
                cancellationToken
            );
        registration.SerializedConfig = secretProtector.Protect(
            hoster.SerializeHosterConfig(mergedConfiguration)
        );
        registration.HasUnreadableSecrets = false;

        await writeRepository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);

        var hoster = hosterFactory.GetByName(registration.HosterClassName);
        var config = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        try
        {
            TryLoginResult result;

            using (HosterRegistrationProxyScope.EnterForUpload(registration))
            {
                result = await hoster.TryLoginAsync(config, cancellationToken);
            }

            if (result.IsSuccess)
            {
                HosterCaptchaVerificationService.Clear(registration, activate: false);
                await writeRepository.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
        catch (CaptchaVerificationRequiredException ex)
        {
            await MarkCaptchaVerificationRequiredAsync(registration, ex.Message, cancellationToken);

            return new TryLoginResult(IsSuccess: false, ErrorMessage: ex.Message);
        }
    }

    public async Task<CaptchaChallengeResult> RequestCaptchaChallengeAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);

        var (hoster, captchaHoster) = GetCaptchaHoster(registration);
        var config = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(registration);

        return await captchaHoster.RequestCaptchaChallengeAsync(config, cancellationToken);
    }

    public async Task<TryLoginResult> VerifyCaptchaAsync(
        int id,
        string challenge,
        string response,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);

        var (hoster, captchaHoster) = GetCaptchaHoster(registration);
        var config = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );
        TryLoginResult result;

        using (HosterRegistrationProxyScope.EnterForUpload(registration))
        {
            result = await captchaHoster.VerifyCaptchaAsync(
                config,
                challenge,
                response,
                cancellationToken
            );
        }

        if (result.IsSuccess)
        {
            HosterCaptchaVerificationService.Clear(registration, activate: true);
            await writeRepository.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public async Task MarkCaptchaVerificationRequiredAsync(
        int id,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        await MarkCaptchaVerificationRequiredAsync(registration, message, cancellationToken);
    }

    private static ProxySelection GetEffectiveMirrorDownloadProxySelection(
        IHoster hoster,
        ProxySelection mirrorDownloadProxySelection
    )
    {
        return hoster is IHosterWithDownload
            ? mirrorDownloadProxySelection
            : ProxySelection.UseCategoryDefault;
    }

    private (IHoster Hoster, IHosterWithCaptchaVerification CaptchaHoster) GetCaptchaHoster(
        HosterRegistration registration
    )
    {
        var hoster = hosterFactory.GetByName(registration.HosterClassName);

        var captchaHoster =
            hoster as IHosterWithCaptchaVerification
            ?? throw new InvalidOperationException(
                $"Hoster {hoster.Name} does not support captcha verification."
            );

        return (hoster, captchaHoster);
    }

    private async Task MarkCaptchaVerificationRequiredAsync(
        HosterRegistration registration,
        string message,
        CancellationToken cancellationToken
    )
    {
        await captchaVerificationService.MarkRequiredAsync(
            registration,
            message,
            cancellationToken
        );

        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}
