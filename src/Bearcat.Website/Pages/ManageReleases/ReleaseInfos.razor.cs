using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseInfos(IReleaseReadRepository readRepository) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    private IReadOnlyList<ReleaseInfoDto> releaseInfos = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseInfosAsync();
    }

    private async Task LoadReleaseInfosAsync()
    {
        isLoading = true;

        try
        {
            releaseInfos = await readRepository.GetReleaseInfosAsync(ReleaseId);
        }
        finally
        {
            isLoading = false;
        }
    }

    private static string GetSizeLabel(ReleaseInfoDto releaseInfo)
    {
        if (releaseInfo.SizeNumber is null && string.IsNullOrWhiteSpace(releaseInfo.SizeUnit))
        {
            return "-";
        }

        return $"{releaseInfo.SizeNumber?.ToString() ?? "-"} {releaseInfo.SizeUnit}".Trim();
    }

    private static string GetValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value;

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

    private string GetUrlLabel(ReleaseExternalInfoUrlDto url)
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
