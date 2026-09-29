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

    private string Title =>
        Snapshot.Identifier.Type switch
        {
            TransferType.RemoteDownloadVerification => L["VerificationStatus"],
            TransferType.RemoteDownloadExtraction => L["ExtractionStatus"],
            TransferType.ArchiveCreation => L["PackingStatus"],
            TransferType.ArchiveHashing => L["HashingStatus"],
            _ => L["DownloadStatus"],
        };

    private string FileProgressLabel =>
        Snapshot.Identifier.Type switch
        {
            TransferType.RemoteDownloadVerification => L["VerificationFileStatus"],
            TransferType.RemoteDownloadExtraction => L["ExtractionArchiveStatus"],
            TransferType.ArchiveCreation => L["PackingProgress"],
            TransferType.ArchiveHashing => L["HashingFileStatus"],
            _ => L["DownloadFileStatus"],
        };

    private static IReadOnlyList<string> GetProxyServerNames(TransferFileProgressSnapshot file)
    {
        return file.ProxyServerName is null ? [] : [file.ProxyServerName];
    }

    private static string FormatTransferred(TransferFileProgressSnapshot file)
    {
        var transferred = TransferFormatting.FormatBytes(file.TransferredBytes);

        return file.TotalBytes <= 0
            ? transferred
            : $"{transferred} / {TransferFormatting.FormatBytes(file.TotalBytes)}";
    }
}
