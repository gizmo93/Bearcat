using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.Home.Uploads;

public static class UploadPartListBuilder
{
    public static IReadOnlyList<UploadPart> Build(
        IReadOnlyList<RunningUploadReadModel.ArchiveFileReadModel> archiveFiles,
        TransferProgressSnapshot? snapshot
    )
    {
        var progressByFileName =
            snapshot?.Files.ToDictionary(file => file.FileName, StringComparer.Ordinal)
            ?? new Dictionary<string, TransferFileProgressSnapshot>(StringComparer.Ordinal);

        return archiveFiles
            .Select(archiveFile =>
            {
                var fileName = Path.GetFileName(archiveFile.FullFileName);
                var progress = progressByFileName.GetValueOrDefault(fileName);

                return new UploadPart(
                    FileName: fileName,
                    State: GetState(archiveFile.UploadedFileOnlineState, progress),
                    OnlineState: archiveFile.UploadedFileOnlineState,
                    Progress: progress
                );
            })
            .ToList();
    }

    private static UploadPartState GetState(
        OnlineState? onlineState,
        TransferFileProgressSnapshot? progress
    )
    {
        return onlineState switch
        {
            OnlineState.Online => UploadPartState.Online,
            not null => UploadPartState.Offline,
            null when progress is { TransferredBytes: > 0 } => UploadPartState.Uploading,
            null => UploadPartState.Pending,
        };
    }
}
