using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseInfoPanel(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    public string ReleaseName { get; set; } = string.Empty;

    [Parameter]
    public ReleaseType ReleaseType { get; set; }

    private ReleaseInfoReadModel? releaseInfo;
    private ReleaseNfoReadModel? releaseNfo;
    private IReadOnlyList<ReleaseMediaFileReadModel> mediaFiles = [];
    private bool isLoading;
    private bool isResolving;
    private bool isExtracting;

    private bool CanExtractMediaMetadata => ReleaseType == ReleaseType.Managed;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseInfoAsync();
    }

    private async Task LoadReleaseInfoAsync()
    {
        isLoading = true;

        try
        {
            await operationRunner.RunAsync<IReleaseReadRepository>(async repository =>
            {
                releaseInfo = await repository.GetReleaseInfoAsync(ReleaseId);
                releaseNfo = await repository.GetReleaseNfoAsync(ReleaseId);
                mediaFiles = await repository.GetMediaFilesAsync(ReleaseId);
            });
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ExtractMediaMetadataAsync()
    {
        if (isExtracting || !CanExtractMediaMetadata)
        {
            return;
        }

        isExtracting = true;

        try
        {
            await operationRunner.RunAsync(
                (MediaMetadataService service) => service.ExtractForReleaseAsync(ReleaseId)
            );

            toastService.Success(L["MediaMetadataExtracted"]);
            await LoadReleaseInfoAsync();
        }
        finally
        {
            isExtracting = false;
        }
    }

    private async Task ShowEditReleaseInfoDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditReleaseInfoDialog.ReleaseId)] = ReleaseId,
            [nameof(EditReleaseInfoDialog.ReleaseName)] = ReleaseName,
            [nameof(EditReleaseInfoDialog.ReleaseInfo)] = releaseInfo,
        };

        var dialog = await dialogService.OpenAsync<EditReleaseInfoDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = releaseInfo is null ? L["AddReleaseInfo"] : L["EditReleaseInfo"],
                Description = L["EditReleaseInfoDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["ReleaseInfoUpdated"]);
        await LoadReleaseInfoAsync();
    }

    private async Task ResolveReleaseInfoAsync()
    {
        if (isResolving)
        {
            return;
        }

        isResolving = true;

        try
        {
            var resolved = await operationRunner.RunAsync(
                (ReleaseInfoResolutionService service) => service.ResolveAsync(ReleaseId)
            );

            if (resolved)
            {
                toastService.Success(L["ReleaseInfoResolved"]);
                await LoadReleaseInfoAsync();
            }
            else
            {
                toastService.Info(L["ReleaseInfoNotResolved"]);
            }
        }
        finally
        {
            isResolving = false;
        }
    }

    private async Task ShowEditReleaseNfoDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditReleaseNfoDialog.ReleaseId)] = ReleaseId,
            [nameof(EditReleaseNfoDialog.ReleaseName)] = ReleaseName,
            [nameof(EditReleaseNfoDialog.ReleaseNfo)] = releaseNfo,
        };

        var dialog = await dialogService.OpenAsync<EditReleaseNfoDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = releaseNfo is null ? L["AddNfo"] : L["EditNfo"],
                Description = L["EditNfoDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["NfoUpdated"]);
        await LoadReleaseInfoAsync();
    }

    private async Task DeleteReleaseInfoAsync(ReleaseInfoReadModel releaseInfo)
    {
        if (releaseInfo.ReleaseInfoId is null)
        {
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseInfoTitle"],
            L["DeleteReleaseInfoConfirmation", releaseInfo.ReleaseName],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (ReleaseInfoService service) => service.DeleteAsync(releaseInfo.ReleaseInfoId.Value)
        );

        toastService.Success(L["ReleaseInfoDeleted"]);
        await LoadReleaseInfoAsync();
    }

    private static string GetSizeLabel(ReleaseInfoReadModel releaseInfo)
    {
        if (releaseInfo.SizeNumber is null && string.IsNullOrWhiteSpace(releaseInfo.SizeUnit))
        {
            return "-";
        }

        return $"{releaseInfo.SizeNumber?.ToString() ?? "-"} {releaseInfo.SizeUnit}".Trim();
    }

    private static string GetValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

    private string GetNfoFileName() =>
        string.IsNullOrWhiteSpace(releaseNfo?.FileName) ? "-" : releaseNfo.FileName;

    private static string GetDatabaseDisplayName(string className)
    {
        if (className.Equals("XrelNfoDatabase", StringComparison.OrdinalIgnoreCase))
        {
            return "xREL";
        }

        const string suffix = "NfoDatabase";
        return className.EndsWith(suffix, StringComparison.Ordinal)
            ? className[..^suffix.Length]
            : className;
    }

    private string GetUrlLabel(ReleaseExternalInfoUrlReadModel url)
    {
        if (url.Type == UrlType.Imdb)
        {
            return LocalizeUrlType(url.Type);
        }

        return
            Uri.TryCreate(url.Url, UriKind.Absolute, out var uri)
            && uri.Host.Contains("xrel.to", StringComparison.OrdinalIgnoreCase)
            ? "xREL"
            : LocalizeUrlType(url.Type);
    }

    private string LocalizeExternalInfoType(ExternalInfoType type) => L[$"ExternalInfoType.{type}"];

    private string LocalizeUrlType(UrlType type) => L[$"UrlType.{type}"];
}
