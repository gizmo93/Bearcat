using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;

namespace Bearcat.Domain.UseCases.ManageHosters;

public class HosterRegistrationService(
    IHosterConfigurationWriteRepository writeRepository,
    IHosterFactory hosterFactory,
    HosterCaptchaVerificationService captchaVerificationService
)
{
    public async Task<int> RegisterHosterAsync(
        string name,
        bool isActive,
        Dictionary<string, string> configuration,
        string hosterClassName,
        CancellationToken cancellationToken = default
    )
    {
        var hoster = hosterFactory.GetByName(hosterClassName);
        var serializedConfig = hoster.SerializeHosterConfig(configuration);

        var registration = new HosterRegistration
        {
            Name = name,
            IsActive = isActive,
            SerializedConfig = serializedConfig,
            HosterClassName = hosterClassName,
        };

        writeRepository.Add(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
        return registration.Id;
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
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var hoster = hosterFactory.GetByName(registration.HosterClassName);

        registration.Name = name;
        registration.SerializedConfig = hoster.SerializeHosterConfig(configuration);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var hoster = hosterFactory.GetByName(registration.HosterClassName);
        var config = hoster.DeserializeHosterConfig(registration.SerializedConfig);

        try
        {
            var result = await hoster.TryLoginAsync(config, cancellationToken);

            if (result.IsSuccess)
            {
                captchaVerificationService.Clear(registration, activate: false);
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
        var config = hoster.DeserializeHosterConfig(registration.SerializedConfig);

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
        var config = hoster.DeserializeHosterConfig(registration.SerializedConfig);
        var result = await captchaHoster.VerifyCaptchaAsync(
            config,
            challenge,
            response,
            cancellationToken
        );

        if (result.IsSuccess)
        {
            captchaVerificationService.Clear(registration, activate: true);
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
