using Bearcat.Website.Pages.ManageArchives;
using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public partial class ArchiveConfigContent(
    IDialogService dialogService)
{
    [Parameter]
    [EditorRequired]
    public ArchiveConfigDto Config { get; set; } = null!;

    private async Task ShowArchiveDialogAsync(int archiveId)
    {
        var parameters = new DialogParameters<ArchiveDetailDialog>
        {
            { dlg => dlg.ArchiveId, archiveId }
        };

        var dialog = await dialogService.ShowAsync<ArchiveDetailDialog>(
            $"Archive Id {archiveId}",
            parameters,
            new DialogOptions
            {
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
            });

        await dialog.Result;
    }
}
