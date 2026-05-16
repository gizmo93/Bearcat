using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Website.Blueprint.Pages.ManageArchives;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Blueprint.Pages.ManageArchiveConfigs;

public partial class ArchiveConfigContent(DialogService dialogService)
{
    [Parameter]
    [EditorRequired]
    public ArchiveConfigDto Config { get; set; } = null!;

    private async Task ShowArchiveDialogAsync(int archiveId)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ArchiveDetailDialog.ArchiveId)] = archiveId,
        };

        await dialogService.OpenAsync<ArchiveDetailDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = $"Archive {archiveId}",
                Description = "Generated archive files and metadata.",
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );
    }
}
