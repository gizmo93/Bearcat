using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseInfoPanel(
    IReleaseReadRepository readRepository,
    ReleaseInfoService releaseInfoService,
    DialogService dialogService,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    private ReleaseInfoReadModel? releaseInfo;
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseInfoAsync();
    }

    private async Task LoadReleaseInfoAsync()
    {
        isLoading = true;

        try
        {
            releaseInfo = await readRepository.GetReleaseInfoAsync(ReleaseId);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task DeleteReleaseInfoAsync(ReleaseInfoReadModel releaseInfo)
    {
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

        await releaseInfoService.DeleteAsync(releaseInfo.ReleaseInfoId);
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

    private static string GetNfoFileName(ReleaseInfoReadModel releaseInfo) =>
        string.IsNullOrWhiteSpace(releaseInfo.ReleaseNfo?.FileName)
            ? "-"
            : releaseInfo.ReleaseNfo.FileName;

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
