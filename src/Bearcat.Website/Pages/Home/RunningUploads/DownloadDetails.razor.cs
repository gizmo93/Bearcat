using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
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
    public DownloadProgressSnapshot Snapshot { get; set; } = null!;

    private static string FormatTransferred(DownloadFileProgressSnapshot file)
    {
        var downloaded = FormatBytes(file.DownloadedBytes);

        return file.TotalBytes <= 0 ? downloaded : $"{downloaded} / {FormatBytes(file.TotalBytes)}";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes <= 0 ? "0 B" : bytes.Bytes().Humanize("0.0");
    }
}
