using Bearcat.Domain.Shared.Transfers;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Uploads;

public partial class UploadFileList : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<UploadPart> Parts { get; set; } = null!;

    [Parameter]
    public TransferProgressSnapshot? Snapshot { get; set; }

    private bool IsProxyServerShownPerFile => Snapshot is { ProxyServerNames.Count: > 1 };

    private static double GetPercentage(UploadPart part) =>
        part.State switch
        {
            UploadPartState.Online => 100,
            UploadPartState.Offline => 0,
            UploadPartState.Uploading => part.Progress!.Percentage,
            UploadPartState.Pending => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part.State, null),
        };

    private static string? GetProgressIndicatorClass(UploadPartState state) =>
        state is UploadPartState.Online ? "bearcat-activity-file-progress-done" : null;
}
