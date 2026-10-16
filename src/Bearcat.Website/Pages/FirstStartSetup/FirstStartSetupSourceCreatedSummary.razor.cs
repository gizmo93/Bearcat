using System.Globalization;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Website.ScopedOperations;
using Humanizer;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupSourceCreatedSummary(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    FirstStartSetupBrowserState browserState
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public FirstStartSetupCreatedSource CreatedSource { get; set; } = null!;

    [Parameter]
    public EventCallback OnBack { get; set; }

    private int stabilityMinutes;
    private int initialUploadCooldownMinutes;

    private bool IsBlockedByQualityGate =>
        CreatedSource.TestReleaseInitialUploadCreationResult is { IsBlockedByQualityGate: true };

    private string CreatedSourceText =>
        CreatedSource.SourceKind is FirstStartSetupSourceKind.LocalFolder
            ? L["FirstStartSetupFolderSourceCreated", CreatedSource.FolderPath]
            : L[
                "FirstStartSetupRemoteSourceCreated",
                CreatedSource.RemoteSourceAutomationName!,
                CreatedSource.FolderPath
            ];

    private string PickupTimingText =>
        CreatedSource.SourceKind is FirstStartSetupSourceKind.LocalFolder
            ? L[
                "FirstStartSetupFolderSourcePickupTiming",
                FormatMinutes(stabilityMinutes),
                FormatMinutes(initialUploadCooldownMinutes)
            ]
            : L[
                "FirstStartSetupRemoteSourcePickupTiming",
                FormatMinutes(stabilityMinutes),
                FormatMinutes(initialUploadCooldownMinutes)
            ];

    private string InitialUploadCreationResultText
    {
        get
        {
            var result = CreatedSource.TestReleaseInitialUploadCreationResult!;

            if (result.IsBlockedByQualityGate)
            {
                return L["InitialUploadsBlockedByQualityGate"];
            }

            return result.CreatedUploadCount switch
            {
                0 => L["FirstStartSetupTestReleaseHasNoUploadConfigs"],
                1 => L["InitialUploadsCreatedOne"],
                _ => L["InitialUploadsCreated", result.CreatedUploadCount],
            };
        }
    }

    protected override void OnInitialized()
    {
        stabilityMinutes =
            CreatedSource.SourceKind is FirstStartSetupSourceKind.LocalFolder
                ? operationRunner.Run(
                    (IApplicationConfigurationProvider configuration) =>
                        configuration.GetValue<FolderAutomationConfiguration>(c =>
                            c.StabilityMinutes
                        )
                )
                : operationRunner.Run(
                    (IApplicationConfigurationProvider configuration) =>
                        configuration.GetValue<RemoteSourceConfiguration>(c => c.StabilityMinutes)
                );
        initialUploadCooldownMinutes = operationRunner.Run(
            (IApplicationConfigurationProvider configuration) =>
                configuration.GetValue<InitialUploadConfiguration>(c => c.CooldownMinutes)
        );
    }

    private static string FormatMinutes(int minutes)
    {
        return TimeSpan.FromMinutes(minutes).Humanize(culture: CultureInfo.CurrentCulture);
    }

    private async Task GoToActivityAsync()
    {
        await browserState.DisableSetupAutoOpenAsync();
        navigationManager.NavigateTo("/");
    }
}
