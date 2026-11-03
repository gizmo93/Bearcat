using Bearcat.Domain.ValueObjects;
using CsvHelper;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

[CsvRecord]
public partial record ReleaseLinkExportReadModel(
    [property: CsvColumn("Release")] string ReleaseName,
    [property: CsvColumn("ReleaseGroup")] string ReleaseGroupName,
    [property: CsvColumn("Hoster")] string HosterName,
    int UploadId,
    OnlineState UploadOnlineState,
    string FileName,
    string Link,
    string? EmbedLink,
    OnlineState LinkOnlineState,
    [property: CsvFormat("yyyy-MM-dd HH:mm:ss")] DateTime? CheckedAt,
    string? ArchivePassword
);
