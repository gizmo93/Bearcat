using System.Globalization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportDetailPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource pollingCancellation = new();
    private readonly Dictionary<string, int?> chosenHosterRegistrationIds = [];
    private IReadOnlyDictionary<string, string> hosterNamesByClassName =
        new Dictionary<string, string>();
    private LinkCrypterImportDetailReadModel? detail;
    private int? loadedLinkCrypterImportId;
    private bool isLoaded;
    private bool isSubmitting;
    private Task? pollingTask;

    [Parameter]
    public int LinkCrypterImportId { get; set; }

    private bool HasBackgroundWork =>
        detail?.State
            is LinkCrypterImportState.ReadingContainers
                or LinkCrypterImportState.CheckingFiles
                or LinkCrypterImportState.Importing;

    private bool ShowsContainers =>
        detail?.State
            is LinkCrypterImportState.ReadyForReview
                or LinkCrypterImportState.Importing
                or LinkCrypterImportState.Completed
                or LinkCrypterImportState.Failed;

    private bool AllHosterRegistrationsChosen =>
        detail is not null
        && detail.HosterRegistrations.All(hosterRegistration =>
            chosenHosterRegistrationIds.GetValueOrDefault(hosterRegistration.HosterClassName)
                is not null
        );

    protected override void OnInitialized()
    {
        hosterNamesByClassName = operationRunner
            .Run((IHosterFactory factory) => factory.GetHosterReadModels())
            .ToDictionary(hoster => hoster.HosterClassName, hoster => hoster.Name);

        pollingTask = PollWhileBackgroundWorkRunsAsync(pollingCancellation.Token);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (loadedLinkCrypterImportId == LinkCrypterImportId)
        {
            return;
        }

        loadedLinkCrypterImportId = LinkCrypterImportId;
        chosenHosterRegistrationIds.Clear();
        await LoadDetailAsync();
        isLoaded = true;
    }

    private async Task LoadDetailAsync()
    {
        detail = await operationRunner.RunAsync(
            (ILinkCrypterImportReadRepository repository) =>
                repository.GetDetailAsync(LinkCrypterImportId)
        );

        foreach (var hosterRegistration in detail?.HosterRegistrations ?? [])
        {
            chosenHosterRegistrationIds.TryAdd(
                hosterRegistration.HosterClassName,
                hosterRegistration.HosterRegistrationId
            );
        }
    }

    private async Task PollWhileBackgroundWorkRunsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!HasBackgroundWork)
                {
                    continue;
                }

                await InvokeAsync(async () =>
                {
                    await LoadDetailAsync();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    private string GetHosterName(string hosterClassName)
    {
        return hosterNamesByClassName.GetValueOrDefault(hosterClassName, hosterClassName);
    }

    private string GetLanguageDisplayName(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return L["NotSet"];
        }

        var culture = CultureInfo.GetCultureInfo(languageCode);
        return $"{culture.NativeName} ({culture.TwoLetterISOLanguageName})";
    }

    private IReadOnlyList<SelectOption<int?>> GetHosterRegistrationOptions(
        LinkCrypterImportHosterRegistrationReadModel hosterRegistration
    )
    {
        return hosterRegistration
            .Options.Select(option => new SelectOption<int?>(
                option.Id,
                option.IsActive ? option.Name : $"{option.Name} ({L["Inactive"]})"
            ))
            .ToList();
    }

    private async Task CheckFilesAsync()
    {
        var choices = detail!
            .HosterRegistrations.Select(
                hosterRegistration => new LinkCrypterImportHosterRegistrationChoice(
                    HosterClassName: hosterRegistration.HosterClassName,
                    HosterRegistrationId: chosenHosterRegistrationIds[
                        hosterRegistration.HosterClassName
                    ]!.Value
                )
            )
            .ToList();

        await RunAndReloadAsync(
            (LinkCrypterImportService service) =>
                service.SetHosterRegistrationsAsync(LinkCrypterImportId, choices)
        );
    }

    private async Task StartImportAsync()
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["StartLinkCrypterImport"],
            $"{L["StartLinkCrypterImportConfirmation", detail!.SelectedContainerCount]} {L["LinkCrypterImportReleaseInfoHint"]}",
            new ConfirmDialogOptions
            {
                ConfirmText = L["StartLinkCrypterImport"],
                CancelText = L["Cancel"],
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        await RunAndReloadAsync(
            (LinkCrypterImportService service) => service.StartImportAsync(LinkCrypterImportId)
        );
    }

    private async Task RunAndReloadAsync(Func<LinkCrypterImportService, Task> operation)
    {
        isSubmitting = true;

        try
        {
            await operationRunner.RunAsync(operation);
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }
        finally
        {
            isSubmitting = false;
        }

        await LoadDetailAsync();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await pollingCancellation.CancelAsync();

        if (pollingTask is not null)
        {
            await pollingTask;
        }

        pollingCancellation.Dispose();
    }
}
