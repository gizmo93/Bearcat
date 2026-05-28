using Bearcat.Domain.UseCases.ManageHosters;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageHosters;

public partial class CaptchaVerificationDialog : OwningComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public int HosterRegistrationId { get; set; }

    [Parameter]
    public string HosterRegistrationName { get; set; } = string.Empty;

    private string? challenge;
    private string? captchaUrl;
    private string captchaResponse = string.Empty;
    private string? errorMessage;
    private bool isRequestingChallenge;
    private bool isSubmitting;

    private bool CanSubmit =>
        !string.IsNullOrWhiteSpace(challenge) && !string.IsNullOrWhiteSpace(captchaResponse);

    private async Task RequestChallengeAsync()
    {
        errorMessage = null;
        isRequestingChallenge = true;

        try
        {
            var service = ScopedServices.GetRequiredService<HosterRegistrationService>();
            var result = await service.RequestCaptchaChallengeAsync(HosterRegistrationId);

            if (!result.IsSuccess)
            {
                errorMessage = result.ErrorMessage ?? "Captcha challenge request failed.";
                return;
            }

            challenge = result.Challenge;
            captchaUrl = result.CaptchaUrl;
        }
        finally
        {
            isRequestingChallenge = false;
        }
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit)
        {
            return;
        }

        errorMessage = null;
        isSubmitting = true;

        try
        {
            var service = ScopedServices.GetRequiredService<HosterRegistrationService>();
            var result = await service.VerifyCaptchaAsync(
                HosterRegistrationId,
                challenge!,
                captchaResponse
            );

            if (!result.IsSuccess)
            {
                errorMessage = result.ErrorMessage ?? "Captcha verification failed.";
                return;
            }

            await DialogRef.CloseAsync(DialogResult.Ok());
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
