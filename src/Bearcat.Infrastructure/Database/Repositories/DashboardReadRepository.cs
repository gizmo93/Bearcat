using Bearcat.Domain.UseCases.Dashboard.ReadModels;
using Bearcat.Domain.UseCases.Dashboard.Repositories;
using Bearcat.Domain.ValueObjects;
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
                upload.UploadConfig.HosterRegistration.Name,
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
        var releases = await dbRead
            .Releases.Select(release => new
            {
                ActiveUploadConfigsCount = release.UploadConfigs.Count(),
                OnlineUploadConfigsCount = release
                    .UploadConfigs.Where(uploadConfig =>
                        uploadConfig.Uploads.Any(upload => upload.OnlineState == OnlineState.Online)
                    )
                    .Distinct()
                    .Count(),
            })
            .ToListAsync(cancellationToken);

        var counts = releases
            .GroupBy(release =>
                GetOnlineState(release.ActiveUploadConfigsCount, release.OnlineUploadConfigsCount)
            )
            .Select(group => new ReleaseOnlineStateCountReadModel(group.Key, group.Count()))
            .OrderBy(count => count.OnlineState)
            .ToList();

        return new ReleaseOnlineStateSummaryReadModel(releases.Count, counts);
    }

    private static OnlineState GetOnlineState(
        int activeUploadConfigsCount,
        int onlineUploadConfigsCount
    )
    {
        if (activeUploadConfigsCount == 0)
        {
            return OnlineState.Unknown;
        }

        if (activeUploadConfigsCount == onlineUploadConfigsCount)
        {
            return OnlineState.Online;
        }

        return onlineUploadConfigsCount > 0 ? OnlineState.PartiallyOnline : OnlineState.Offline;
    }
}
