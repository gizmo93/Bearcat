using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

public record UploadLinkResponse(
    string FileName,
    string HosterFileLink,
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
            OnlineState: readModel.OnlineState,
            CheckedAt: readModel.CheckedAt,
            ErrorMessages: readModel.ErrorMessages,
            DownloadCount: readModel.DownloadCount
        );
    }
}
