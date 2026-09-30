using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Formatting;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageHosters;

public partial class AddOrEditHoster(IScopedOperationRunner operationRunner) : ComponentBase
{
    [Parameter]
    public HosterFormModel FormModel { get; set; } = new();

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private const decimal MinimumSpeedLimitMegabytesPerSecond = 0.001m;
    private const decimal MaximumSpeedLimitMegabytesPerSecond = 1_000_000m;

    private IReadOnlyList<HosterDto> hosterReadModels = [];
    private HosterDto? selectedHoster;

    private IReadOnlyList<SelectOption<ReuploadTrigger?>> ReuploadTriggerOptions =>
        [
            new(null, L["ReuploadTriggerOverridePlaceholder"]),
            .. Enum.GetValues<ReuploadTrigger>()
                .Select(trigger => new SelectOption<ReuploadTrigger?>(
                    trigger,
                    L.Localize(trigger)
                )),
        ];

    private EditContext editContext = null!;
    private ValidationMessageStore? messageStore;
    private readonly HashSet<string> displayedPasswords = [];

    private bool RequiresAllConfigurationValues =>
        !FormModel.IsEdit || FormModel.HasUnreadableSecrets;

    protected override void OnInitialized()
    {
        hosterReadModels = operationRunner.Run(
            (IHosterFactory factory) => factory.GetHosterReadModels()
        );
        editContext = new EditContext(FormModel);
        editContext.OnValidationRequested += HandleValidationRequested;
        messageStore = new ValidationMessageStore(editContext);

        if (FormModel.IsEdit)
        {
            selectedHoster = hosterReadModels.First(h =>
                h.HosterClassName == FormModel.FullClassName
            );
        }
    }

    private async Task SaveAsync()
    {
        var uploadSpeedLimitMegabytesPerSecond = ParseSpeedLimit(
            FormModel.UploadSpeedLimitMegabytesPerSecondInput
        );
        var mirrorDownloadSpeedLimitMegabytesPerSecond = ParseSpeedLimit(
            FormModel.MirrorDownloadSpeedLimitMegabytesPerSecondInput
        );

        if (!FormModel.IsEdit)
        {
            await operationRunner.RunAsync(
                (HosterRegistrationService service) =>
                    service.RegisterHosterAsync(
                        name: FormModel.Name,
                        isActive: true,
                        configuration: FormModel.Configuration,
                        hosterClassName: FormModel.FullClassName,
                        maxParallelUploadsOverride: FormModel.MaxParallelUploadsOverride,
                        uploadSpeedLimitMegabytesPerSecond: uploadSpeedLimitMegabytesPerSecond,
                        numberOfHoursUntilReuploadOverride: FormModel.NumberOfHoursUntilReuploadOverride,
                        reuploadTriggerOverride: FormModel.ReuploadTriggerOverride,
                        alwaysReuploadAllFiles: FormModel.AlwaysReuploadAllFiles,
                        useForMirrorDownloads: FormModel.UseForMirrorDownloads,
                        mirrorPriority: FormModel.MirrorPriority,
                        mirrorDownloadSpeedLimitMegabytesPerSecond: mirrorDownloadSpeedLimitMegabytesPerSecond,
                        uploadProxySelection: FormModel.UploadProxySelection,
                        uploadProxyServerId: FormModel.UploadProxyServerId,
                        mirrorDownloadProxySelection: FormModel.MirrorDownloadProxySelection,
                        mirrorDownloadProxyServerId: FormModel.MirrorDownloadProxyServerId
                    )
            );
        }
        else
        {
            await operationRunner.RunAsync(
                (HosterRegistrationService service) =>
                    service.UpdateRegistrationAsync(
                        id: FormModel.HosterRegistrationId!.Value,
                        name: FormModel.Name,
                        configuration: FormModel.Configuration,
                        maxParallelUploadsOverride: FormModel.MaxParallelUploadsOverride,
                        uploadSpeedLimitMegabytesPerSecond: uploadSpeedLimitMegabytesPerSecond,
                        numberOfHoursUntilReuploadOverride: FormModel.NumberOfHoursUntilReuploadOverride,
                        reuploadTriggerOverride: FormModel.ReuploadTriggerOverride,
                        alwaysReuploadAllFiles: FormModel.AlwaysReuploadAllFiles,
                        useForMirrorDownloads: FormModel.UseForMirrorDownloads,
                        mirrorPriority: FormModel.MirrorPriority,
                        mirrorDownloadSpeedLimitMegabytesPerSecond: mirrorDownloadSpeedLimitMegabytesPerSecond,
                        uploadProxySelection: FormModel.UploadProxySelection,
                        uploadProxyServerId: FormModel.UploadProxyServerId,
                        mirrorDownloadProxySelection: FormModel.MirrorDownloadProxySelection,
                        mirrorDownloadProxyServerId: FormModel.MirrorDownloadProxyServerId
                    )
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }

        if (selectedHoster is null)
        {
            messageStore.Add(() => FormModel.FullClassName, L["SelectHosterRequired"]);
        }

        if (!IsValidSpeedLimitInput(FormModel.UploadSpeedLimitMegabytesPerSecondInput))
        {
            messageStore.Add(
                new FieldIdentifier(
                    FormModel,
                    nameof(HosterFormModel.UploadSpeedLimitMegabytesPerSecondInput)
                ),
                L["SpeedLimitMegabytesPerSecondInvalid"]
            );
        }

        if (!IsValidSpeedLimitInput(FormModel.MirrorDownloadSpeedLimitMegabytesPerSecondInput))
        {
            messageStore.Add(
                new FieldIdentifier(
                    FormModel,
                    nameof(HosterFormModel.MirrorDownloadSpeedLimitMegabytesPerSecondInput)
                ),
                L["SpeedLimitMegabytesPerSecondInvalid"]
            );
        }

        if (selectedHoster is null)
        {
            return;
        }

        if (!RequiresAllConfigurationValues)
        {
            return;
        }

        var missingConfigs = selectedHoster
            .ConfigurationKeys.Where(key =>
                string.IsNullOrWhiteSpace(FormModel.Configuration.GetValueOrDefault(key))
            )
            .ToList();

        foreach (var config in missingConfigs)
        {
            messageStore.Add(
                () => FormModel.Configuration,
                L["ConfigurationValueRequired", config]
            );
        }
    }

    private static bool IsValidSpeedLimitInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        return DecimalInputConverter.TryParse(input, out var megabytesPerSecond)
            && megabytesPerSecond
                is >= MinimumSpeedLimitMegabytesPerSecond
                    and <= MaximumSpeedLimitMegabytesPerSecond;
    }

    private static decimal? ParseSpeedLimit(string? input)
    {
        return DecimalInputConverter.TryParse(input, out var megabytesPerSecond)
            ? megabytesPerSecond
            : null;
    }

    private void OnSelectedHosterChanged()
    {
        selectedHoster = string.IsNullOrEmpty(FormModel.FullClassName)
            ? null
            : hosterReadModels.First(h => h.HosterClassName == FormModel.FullClassName);

        displayedPasswords.Clear();

        if (selectedHoster is null or { SupportsDownload: false })
        {
            FormModel.UseForMirrorDownloads = false;
            FormModel.MirrorDownloadSpeedLimitMegabytesPerSecondInput = null;
        }

        if (!FormModel.IsEdit)
        {
            FormModel.Configuration = new Dictionary<string, string>();
        }
    }

    private void OnConfigurationValueChanged(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            FormModel.Configuration.Remove(key);
            return;
        }

        FormModel.Configuration[key] = value;
    }

    private void ToggleShowHidePassword(string key)
    {
        if (displayedPasswords.Add(key))
        {
            return;
        }

        displayedPasswords.Remove(key);
    }
}
