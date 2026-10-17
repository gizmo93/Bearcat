using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

/// <param name="FileName">Uploaded file: full path of the archive file, or the path of the video file relative to the release folder for video uploads.</param>
/// <param name="HosterFileLink">Download link of the file on the hoster.</param>
/// <param name="HosterEmbedLink">Embed link of the file on a video hoster, or null for archive uploads.</param>
/// <param name="OnlineState">Online state of the file.</param>
/// <param name="CheckedAt">Time of the last online check, or null.</param>
/// <param name="ErrorMessages">Error messages of the file.</param>
/// <param name="DownloadCount">Download count reported by the hoster, or null when unknown.</param>
public record UploadLinkResponse(
    string FileName,
    string HosterFileLink,
    string? HosterEmbedLink,
    OnlineState OnlineState,
    DateTime? CheckedAt,
    IReadOnlyList<string> ErrorMessages,
    int? DownloadCount
)
{
    public static UploadLinkResponse FromReadModel(ReleaseUploadLinkReadModel readModel)
    {
        return new UploadLinkResponse(
            FileName: readModel.FileName,
            HosterFileLink: readModel.HosterFileLink,
            HosterEmbedLink: readModel.HosterEmbedLink,
            OnlineState: readModel.OnlineState,
            CheckedAt: readModel.CheckedAt,
            ErrorMessages: readModel.ErrorMessages,
            DownloadCount: readModel.DownloadCount
        );
    }
}
