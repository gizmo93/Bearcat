using Bearcat.Domain.Shared.Transfers;
using Bearcat.Website.Formatting;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class TransferFileList : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public TransferProgressSnapshot Snapshot { get; set; } = null!;

    private bool IsProxyServerShownPerFile => Snapshot.ProxyServerNames.Count > 1;

    private static bool IsFileDone(TransferFileProgressSnapshot file) =>
        file.TotalBytes > 0 && file.TransferredBytes >= file.TotalBytes;

    private string FormatFileStatus(TransferFileProgressSnapshot file)
    {
        var transferred = TransferFormatting.FormatBytes(file.TransferredBytes);

        return file.TotalBytes <= 0
            ? transferred
            : $"{file.Percentage}% · {L["TransferredBytesOfTotalBytes", transferred, TransferFormatting.FormatBytes(file.TotalBytes)]}";
    }
}
