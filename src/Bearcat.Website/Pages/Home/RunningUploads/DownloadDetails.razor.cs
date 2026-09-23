using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Humanizer;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class DownloadDetails : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public Archive Archive { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public TransferProgressSnapshot Snapshot { get; set; } = null!;

    private static string FormatTransferred(TransferFileProgressSnapshot file)
    {
        var transferred = FormatBytes(file.TransferredBytes);

        return file.TotalBytes <= 0
            ? transferred
            : $"{transferred} / {FormatBytes(file.TotalBytes)}";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes <= 0 ? "0 B" : bytes.Bytes().Humanize("0.0");
    }
}
