using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Blueprint.Pages.Home.RunningUploads;

public partial class RunningUploads : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Upload> Uploads { get; set; } = null!;

    private readonly HashSet<int> showDetailIds = [];

    private IEnumerable<Upload> SortedUploads => Uploads.OrderByDescending(u => u.UploadState);

    private IReadOnlyList<Upload> ExpandedUploads =>
        SortedUploads.Where(upload => showDetailIds.Contains(upload.Id)).ToList();

    private void ToggleShowUploadDetails(int uploadId)
    {
        if (!showDetailIds.Remove(uploadId))
        {
            showDetailIds.Add(uploadId);
        }

        StateHasChanged();
    }

    private static BadgeVariant GetUploadVariant(UploadState state) =>
        state switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private static double GetUploadProgress(Upload upload)
    {
        var uploadedFiles = upload.UploadedFiles.Count;
        var archiveFiles = upload.Archive?.ArchiveFiles.Count ?? 0;

        if (archiveFiles == 0)
        {
            return 0;
        }

        return Math.Round((double)uploadedFiles / archiveFiles * 100, 0);
    }
}
