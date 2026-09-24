using Bearcat.Domain.Shared.Transfers;
using Bearcat.Website.Formatting;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home;

public partial class TransferFileDetails : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public TransferProgressSnapshot Snapshot { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Subtitle { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string StateLabel { get; set; } = null!;

    private static string FormatTransferred(TransferFileProgressSnapshot file)
    {
        var transferred = TransferFormatting.FormatBytes(file.TransferredBytes);

        return file.TotalBytes <= 0
            ? transferred
            : $"{transferred} / {TransferFormatting.FormatBytes(file.TotalBytes)}";
    }
}
