using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadReadRepository(IBearcatReadDbContext dbRead) : IUploadReadRepository
{
    public async Task<PagedResult<UploadReadModel>> SearchUploadsAsync(
        UploadSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);

        var uploadsQuery = dbRead.Uploads.AsQueryable();

        if (query.UploadedAfter is not null)
        {
            uploadsQuery = uploadsQuery.Where(u =>
                u.UploadedAt != null && u.UploadedAt > query.UploadedAfter.Value
            );
        }

        if (query.UploadState is not null)
        {
            uploadsQuery = uploadsQuery.Where(u => u.UploadState == query.UploadState.Value);
        }

        if (query.OnlineState is not null)
        {
            uploadsQuery = uploadsQuery.Where(u => u.OnlineState == query.OnlineState.Value);
        }

        if (query.HosterRegistrationId is not null)
        {
            uploadsQuery = uploadsQuery.Where(u =>
                u.UploadConfig.HosterRegistrationId == query.HosterRegistrationId.Value
            );
        }

        if (query.ReleaseId is not null)
        {
            uploadsQuery = uploadsQuery.Where(u =>
                u.UploadConfig.ReleaseId == query.ReleaseId.Value
            );
        }

        var totalCount = await uploadsQuery.CountAsync(cancellationToken);

        var orderedQuery = query.UploadedAfter is not null
            ? uploadsQuery.OrderBy(u => u.UploadedAt).ThenBy(u => u.Id)
            : uploadsQuery.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id);

        var uploads = await orderedQuery
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(ToReadModel)
            .ToListAsync(cancellationToken: cancellationToken);

        return new PagedResult<UploadReadModel>(uploads, totalCount, pageIndex, pageSize);
    }

    public async Task<UploadReadModel?> GetUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Uploads.Where(u => u.Id == uploadId)
            .Select(ToReadModel)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static readonly Expression<Func<Upload, UploadReadModel>> ToReadModel =
        u => new UploadReadModel(
            u.Id,
            u.UploadConfig.ReleaseId,
            u.UploadConfig.Release.Name,
            u.UploadConfigId,
            u.UploadConfig.Name,
            u.UploadConfig.HosterRegistrationId,
            u.UploadConfig.HosterRegistration.Name,
            u.CreatedAt,
            u.UploadedAt,
            u.UploadState,
            u.OnlineState,
            u.NotFullyOnlineSince,
            u.FullyOfflineSince,
            u.UploadedFiles.Count,
            u.ErrorMessages.ToList()
        );
}
