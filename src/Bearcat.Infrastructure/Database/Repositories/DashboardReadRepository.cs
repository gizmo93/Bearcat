using Bearcat.Domain.UseCases.Dashboard.ReadModels;
using Bearcat.Domain.UseCases.Dashboard.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class DashboardReadRepository(IBearcatReadDbContext dbRead) : IDashboardReadRepository
{
    public async Task<IReadOnlyList<UploadDayReadModel>> GetUploadsPerDayAsync(
        DateOnly? uploadedFrom = null,
        DateOnly? uploadedTo = null,
        CancellationToken cancellationToken = default
    )
    {
        var uploadsQuery = dbRead.Uploads.Where(upload => upload.UploadedAt != null);

        if (uploadedFrom is not null)
        {
            var from = uploadedFrom.Value.ToDateTime(TimeOnly.MinValue);
            uploadsQuery = uploadsQuery.Where(upload => upload.UploadedAt >= from);
        }

        if (uploadedTo is not null)
        {
            var toExclusive = uploadedTo.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
            uploadsQuery = uploadsQuery.Where(upload => upload.UploadedAt < toExclusive);
        }

        var uploads = await uploadsQuery
            .GroupBy(upload => new
            {
                Day = upload.UploadedAt!.Value.Date,
                upload.HosterRegistration.Name,
            })
            .Select(group => new
            {
                group.Key.Day,
                group.Key.Name,
                UploadCount = group.Count(),
            })
            .OrderBy(upload => upload.Day)
            .ThenBy(upload => upload.Name)
            .ToListAsync(cancellationToken);

        return uploads
            .Select(upload => new UploadDayReadModel(
                DateOnly.FromDateTime(upload.Day),
                upload.Name,
                upload.UploadCount
            ))
            .ToList();
    }

    public async Task<ReleaseOnlineStateSummaryReadModel> GetReleaseOnlineStateSummaryAsync(
        CancellationToken cancellationToken = default
    )
    {
        var releaseCountPerOnlineState = await dbRead
            .Releases.GroupBy(release => release.OnlineState)
            .Select(group => new { OnlineState = group.Key, ReleaseCount = group.Count() })
            .OrderBy(count => count.OnlineState)
            .ToListAsync(cancellationToken);

        var counts = releaseCountPerOnlineState
            .Select(count => new ReleaseOnlineStateCountReadModel(
                count.OnlineState,
                count.ReleaseCount
            ))
            .ToList();

        return new ReleaseOnlineStateSummaryReadModel(
            counts.Sum(count => count.ReleaseCount),
            counts
        );
    }
}
