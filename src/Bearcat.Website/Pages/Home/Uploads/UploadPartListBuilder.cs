using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.Home.Uploads;

public static class UploadPartListBuilder
{
    public static IReadOnlyList<UploadPart> Build(
        IReadOnlyList<RunningUploadReadModel.FileReadModel> files,
        TransferProgressSnapshot? snapshot
    )
    {
        if (files.Count == 0 && snapshot is not null)
        {
            return snapshot
                .Files.Select(progress => new UploadPart(
                    FileName: progress.FileName,
                    State: GetState(onlineState: null, progress),
                    OnlineState: null,
                    Progress: progress
                ))
                .ToList();
        }

        var progressByFileName =
            snapshot?.Files.ToDictionary(file => file.FileName, StringComparer.Ordinal)
            ?? new Dictionary<string, TransferFileProgressSnapshot>(StringComparer.Ordinal);

        return files
            .Select(file =>
            {
                var fileName = Path.GetFileName(file.FilePath);
                var progress = progressByFileName.GetValueOrDefault(fileName);

                return new UploadPart(
                    FileName: fileName,
                    State: GetState(file.UploadedFileOnlineState, progress),
                    OnlineState: file.UploadedFileOnlineState,
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
