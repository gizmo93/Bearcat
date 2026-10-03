using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
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

        if (query.ReleaseGroupId is not null)
        {
            uploadsQuery = uploadsQuery.Where(u =>
                u.UploadConfig.Release.ReleaseGroupId == query.ReleaseGroupId.Value
            );
        }

        var searchTerm = SearchTextPatterns.TrimOrNullWhenEmpty(query.SearchTerm);

        if (searchTerm is not null)
        {
            uploadsQuery = ApplySearchTerm(uploadsQuery, searchTerm);
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

    public async Task<IReadOnlyList<RunningUploadReadModel>> GetRunningUploadsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var uploads = await dbRead
            .Uploads.Where(u =>
                u.UploadState == UploadState.Pending
                || u.UploadState == UploadState.Uploading
                || u.UploadState == UploadState.CancellationRequested
            )
            .Select(u => new
            {
                u.Id,
                u.UploadConfig.ReleaseId,
                ReleaseName = u.UploadConfig.Release.Name,
                UploadConfigName = u.UploadConfig.Name,
                HosterRegistrationName = u.UploadConfig.HosterRegistration.Name,
                u.UploadState,
                ArchiveFiles = dbRead
                    .ArchiveFiles.Where(af => af.ArchiveId == u.ArchiveId)
                    .Select(af => new { af.Id, af.FullFileName })
                    .ToList(),
                UploadedFiles = u
                    .UploadedFiles.Select(uf => new
                    {
                        uf.Id,
                        uf.ArchiveFileId,
                        uf.OnlineState,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return uploads
            .Select(upload => new RunningUploadReadModel(
                upload.Id,
                upload.ReleaseId,
                upload.ReleaseName,
                upload.UploadConfigName,
                upload.HosterRegistrationName,
                upload.UploadState,
                upload
                    .ArchiveFiles.OrderBy(
                        archiveFile => archiveFile.FullFileName,
                        StringComparer.Ordinal
                    )
                    .Select(archiveFile => new RunningUploadReadModel.ArchiveFileReadModel(
                        archiveFile.FullFileName,
                        upload
                            .UploadedFiles.Where(uploadedFile =>
                                uploadedFile.ArchiveFileId == archiveFile.Id
                            )
                            .OrderBy(uploadedFile => uploadedFile.Id)
                            .Select(uploadedFile => (OnlineState?)uploadedFile.OnlineState)
                            .FirstOrDefault()
                    ))
                    .ToList()
            ))
            .ToList();
    }

    private static IQueryable<Upload> ApplySearchTerm(IQueryable<Upload> uploads, string searchTerm)
    {
        var pattern = SearchTextPatterns.ToLowerCaseContainsPattern(searchTerm);
        var uploadId = SearchTextPatterns.ParseIdWithOptionalHashPrefix(searchTerm);

        return uploads.Where(u =>
            u.Id == uploadId
            || EF.Functions.Like(u.UploadConfig.Release.Name.ToLower(), pattern)
            || u.UploadedFiles.Any(file =>
                EF.Functions.Like(file.HosterFileLink.ToLower(), pattern)
            )
        );
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
            u.ErrorMessages,
            u.ArchiveId
        );
}
