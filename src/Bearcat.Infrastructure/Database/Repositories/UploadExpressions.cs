using System.Linq.Expressions;
using Bearcat.Domain.Entities;

namespace Bearcat.Infrastructure.Database.Repositories;

internal static class UploadExpressions
{
    public static Expression<Func<Upload, bool>> UploadIsReupload(IBearcatReadDbContext dbRead)
    {
        return upload =>
            dbRead.Uploads.Any(otherUpload =>
                otherUpload.UploadConfig.ReleaseId == upload.UploadConfig.ReleaseId
                && otherUpload.UploadedAt != null
                && otherUpload.UploadedAt < upload.CreatedAt
            );
    }
}
