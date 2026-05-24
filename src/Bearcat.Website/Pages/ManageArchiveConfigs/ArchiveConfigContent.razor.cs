using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageArchives;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public partial class ArchiveConfigContent(DialogService dialogService)
{
    [Parameter]
    [EditorRequired]
    public ArchiveConfigReadModel Config { get; set; } = null!;

    private static BadgeVariant GetArchiveVariant(ArchiveState state) =>
        state switch
        {
            ArchiveState.Created => BadgeVariant.Default,
            ArchiveState.CreationFailed => BadgeVariant.Destructive,
            ArchiveState.MissingFiles => BadgeVariant.Destructive,
            ArchiveState.Creating => BadgeVariant.Secondary,
            _ => BadgeVariant.Outline,
        };

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
                Title = L["ArchiveTitle", archiveId],
                Description = L["ArchiveDetailDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );
    }
}
