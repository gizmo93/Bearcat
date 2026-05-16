using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Blueprint.Localization;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Blueprint.Pages.Home.RunningUploads;

public partial class FileDetails : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public Upload Upload { get; set; } = null!;

    private string GetStatusLabel(ArchiveFile archiveFile)
    {
        var uploadedFile = Upload.UploadedFiles.FirstOrDefault(x =>
            x.ArchiveFileId == archiveFile.Id
        );
        return uploadedFile is null ? L["Pending"] : L.Localize(uploadedFile.OnlineState);
    }

    private BadgeVariant GetSummaryVariant() =>
        Upload.UploadState switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
}
