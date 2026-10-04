using System.Globalization;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
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
    private ReleaseMetadataReadModel? releaseMetadata;
    private ReleaseNfoReadModel? releaseNfo;
    private IReadOnlyList<ReleaseExternalIdentifierReadModel> externalIdentifiers = [];
    private IReadOnlyList<ReleaseMediaFileReadModel> mediaFiles = [];
    private ReleaseClassificationReadModel? classification;
    private bool isLoading;
    private bool isResolving;
    private bool isExtracting;

    private bool CanExtractMediaMetadata => ReleaseType == ReleaseType.Managed;

    private bool HasCover => !string.IsNullOrWhiteSpace(releaseMetadata?.CoverUrl);

    private string CoverDownloadUrl => $"/releases/{ReleaseId}/cover";

    private string CoverDownloadFileName => GetCoverDownloadFileName();

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
                releaseMetadata = await repository.GetReleaseMetadataAsync(ReleaseId);
                releaseNfo = await repository.GetReleaseNfoAsync(ReleaseId);
                externalIdentifiers = await repository.GetReleaseExternalIdentifiersAsync(
                    ReleaseId
                );
                mediaFiles = await repository.GetMediaFilesAsync(ReleaseId);
                classification = await repository.GetClassificationAsync(ReleaseId);
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
            [nameof(EditReleaseInfoDialog.ReleaseMetadata)] = releaseMetadata,
            [nameof(EditReleaseInfoDialog.ExternalIdentifiers)] = externalIdentifiers,
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
                toastService.Success(L["ReleaseMetadataResolved"]);
                await LoadReleaseInfoAsync();
            }
            else
            {
                toastService.Info(L["ReleaseMetadataNotResolved"]);
            }
        }
        finally
        {
            isResolving = false;
        }
    }

    private async Task RefreshMetadataAsync()
    {
        if (isResolving)
        {
            return;
        }

        isResolving = true;

        try
        {
            var resolved = await operationRunner.RunAsync(
                (ReleaseInfoResolutionService service) => service.RefreshMetadataAsync(ReleaseId)
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

    private async Task DeleteReleaseInfoAsync()
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseInfoTitle"],
            L["DeleteReleaseInfoConfirmation", ReleaseName],
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
            (ReleaseInfoService service) => service.DeleteAsync(ReleaseId)
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

        return $"{releaseInfo.SizeNumber?.ToString(CultureInfo.CurrentCulture) ?? "-"} {releaseInfo.SizeUnit}".Trim();
    }

    private static string GetValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

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

    private string GetCoverDownloadFileName()
    {
        if (Uri.TryCreate(releaseMetadata?.CoverUrl, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return $"{SanitizeFileName(ReleaseName)}-cover.jpg";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(
            value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()
        );

        return string.IsNullOrWhiteSpace(sanitized) ? "release" : sanitized;
    }

    private string LocalizeExternalInfoType(ExternalInfoType type) => L[$"ExternalInfoType.{type}"];

    private string LocalizeUrlType(UrlType type) => L[$"UrlType.{type}"];
}
