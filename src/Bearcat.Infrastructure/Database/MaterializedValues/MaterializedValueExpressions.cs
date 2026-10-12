using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Infrastructure.Database.MaterializedValues;

public static class MaterializedValueExpressions
{
    public static readonly Expression<Func<Release, OnlineState>> ReleaseOnlineState = release =>
        !release.UploadConfigs.Any() ? OnlineState.Unknown
        : release.UploadConfigs.All(uploadConfig =>
            uploadConfig.Uploads.Any(upload => upload.OnlineState == OnlineState.Online)
        )
            ? OnlineState.Online
        : release.UploadConfigs.Any(uploadConfig =>
            uploadConfig.Uploads.Any(upload => upload.OnlineState == OnlineState.Online)
        )
            ? OnlineState.PartiallyOnline
        : OnlineState.Offline;

    public static readonly Expression<
        Func<Upload, DateTime?>
    > OldestUploadedFileCheckedAtWithNeverCheckedFilesAsMinValue = upload =>
        upload.UploadedFiles.Min(uploadedFile =>
            (DateTime?)(uploadedFile.CheckedAt ?? DateTime.MinValue)
        );
}
