using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class FileDetails : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public RunningUploadReadModel Upload { get; set; } = null!;

    private string GetStatusLabel(RunningUploadReadModel.ArchiveFileReadModel archiveFile)
    {
        return archiveFile.UploadedFileOnlineState is { } onlineState
            ? L.Localize(onlineState)
            : L["Pending"];
    }

    private BadgeVariant GetSummaryVariant() =>
        Upload.UploadState switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.CancellationRequested => BadgeVariant.Secondary,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
}
