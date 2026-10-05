using Bearcat.Domain.Shared.Transfers;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Uploads;

public partial class UploadPartSegments : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<UploadPart> Parts { get; set; } = null!;

    [Parameter]
    public TransferProgressSnapshot? Snapshot { get; set; }

    private int OnlinePartCount => Parts.Count(part => part.State is UploadPartState.Online);

    private static string GetSegmentClass(UploadPartState state) =>
        state switch
        {
            UploadPartState.Online => "bearcat-overview-online-segment-online",
            UploadPartState.Offline => "bearcat-overview-online-segment-offline",
            UploadPartState.Uploading => "bearcat-activity-segment-uploading",
            UploadPartState.Pending => "bearcat-overview-online-segment-unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
}
