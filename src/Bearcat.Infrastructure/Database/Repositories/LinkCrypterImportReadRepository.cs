using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterImportReadRepository(IBearcatReadDbContext dbRead)
    : ILinkCrypterImportReadRepository
{
    public async Task<IReadOnlyList<LinkCrypterImportReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .LinkCrypterImports.OrderByDescending(import => import.CreatedAt)
            .ThenByDescending(import => import.Id)
            .Select(import => new LinkCrypterImportReadModel(
                Id: import.Id,
                LinkCrypterRegistrationName: import.LinkCrypterRegistration.Name,
                ReleaseTemplateName: import.ReleaseTemplate.Name,
                State: import.State,
                CreatedAt: import.CreatedAt,
                UpdatedAt: import.UpdatedAt,
                ContainerCount: import.Containers.Count,
                ProcessedContainerCount: import.ProcessedContainerCount,
                ContainerCountToProcess: import.ContainerCountToProcess
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterImportDetailReadModel?> GetDetailAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken = default
    )
    {
        var import = await dbRead
            .LinkCrypterImports.Where(import => import.Id == linkCrypterImportId)
            .Select(import => new
            {
                import.Id,
                import.LinkCrypterRegistrationId,
                LinkCrypterRegistrationName = import.LinkCrypterRegistration.Name,
                import.ReleaseTemplateId,
                ReleaseTemplateName = import.ReleaseTemplate.Name,
                import.ArchiveFilesBasePathForReleasesWithoutLocalFiles,
                import.PrimaryLanguageCode,
                import.State,
                import.CreatedAt,
                import.UpdatedAt,
                import.ProcessedContainerCount,
                import.ContainerCountToProcess,
                import.ErrorMessages,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (import is null)
        {
            return null;
        }

        var containerCountsByStatus = await dbRead
            .LinkCrypterImportContainers.Where(container =>
                container.LinkCrypterImportId == linkCrypterImportId
            )
            .GroupBy(container => container.Status)
            .Select(group => new LinkCrypterImportContainerStatusCountReadModel(
                group.Key,
                group.Count()
            ))
            .ToListAsync(cancellationToken);
        var selectedContainerCount = await dbRead.LinkCrypterImportContainers.CountAsync(
            container =>
                container.LinkCrypterImportId == linkCrypterImportId && container.IsSelected,
            cancellationToken
        );
        var selectedReleaseCount = await dbRead
            .LinkCrypterImportContainers.Where(container =>
                container.LinkCrypterImportId == linkCrypterImportId && container.IsSelected
            )
            .Select(container => container.ReleaseName.ToLower())
            .Distinct()
            .CountAsync(cancellationToken);

        return new LinkCrypterImportDetailReadModel(
            Id: import.Id,
            LinkCrypterRegistrationId: import.LinkCrypterRegistrationId,
            LinkCrypterRegistrationName: import.LinkCrypterRegistrationName,
            ReleaseTemplateId: import.ReleaseTemplateId,
            ReleaseTemplateName: import.ReleaseTemplateName,
            ArchiveFilesBasePathForReleasesWithoutLocalFiles: import.ArchiveFilesBasePathForReleasesWithoutLocalFiles,
            PrimaryLanguageCode: import.PrimaryLanguageCode,
            State: import.State,
            CreatedAt: import.CreatedAt,
            UpdatedAt: import.UpdatedAt,
            ProcessedContainerCount: import.ProcessedContainerCount,
            ContainerCountToProcess: import.ContainerCountToProcess,
            SelectedContainerCount: selectedContainerCount,
            SelectedReleaseCount: selectedReleaseCount,
            ErrorMessages: import.ErrorMessages,
            ContainerCountsByStatus: containerCountsByStatus
                .OrderBy(count => count.Status)
                .ToList(),
            HosterRegistrations: await GetHosterRegistrationsAsync(
                linkCrypterImportId,
                cancellationToken
            )
        );
    }

    public async Task<PagedResult<LinkCrypterImportReleaseReadModel>> SearchReleasesAsync(
        LinkCrypterImportReleaseSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);
        var containersOfImport = dbRead.LinkCrypterImportContainers.Where(container =>
            container.LinkCrypterImportId == query.LinkCrypterImportId
        );
        var matchingContainers = containersOfImport;

        if (query.Statuses.Count > 0)
        {
            matchingContainers = matchingContainers.Where(container =>
                EF.Parameter(query.Statuses).Contains(container.Status)
            );
        }

        var releasesQuery = matchingContainers
            .GroupBy(container => container.ReleaseName.ToLower())
            .Select(group => new
            {
                LowerReleaseName = group.Key,
                LowestContainerId = group.Min(container => container.Id),
            });

        var totalCount = await releasesQuery.CountAsync(cancellationToken);
        var lowerReleaseNamesOfPage = await releasesQuery
            .OrderBy(release => release.LowestContainerId)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(release => release.LowerReleaseName)
            .ToListAsync(cancellationToken);

        var containers = await containersOfImport
            .Where(container => lowerReleaseNamesOfPage.Contains(container.ReleaseName.ToLower()))
            .OrderBy(container => container.Id)
            .Select(container => new
            {
                container.Id,
                container.ContainerName,
                container.ReleaseName,
                container.ContainerUrl,
                container.HasBackupLinks,
                container.IsSelected,
                container.Status,
                container.NotImportableReason,
                container.ImportedReleaseId,
                container.ErrorMessages,
                container.PreviewJson,
            })
            .ToListAsync(cancellationToken);

        var releases = containers
            .Select(container => new
            {
                container.ReleaseName,
                ReadModel = new LinkCrypterImportContainerReadModel(
                    Id: container.Id,
                    ContainerName: container.ContainerName,
                    ContainerUrl: container.ContainerUrl,
                    HasBackupLinks: container.HasBackupLinks,
                    IsSelected: container.IsSelected,
                    Status: container.Status,
                    NotImportableReason: container.NotImportableReason,
                    ImportedReleaseId: container.ImportedReleaseId,
                    ErrorMessages: container.ErrorMessages,
                    Preview: LinkCrypterImportContainerPreview.FromJson(container.PreviewJson)
                ),
            })
            .GroupBy(container => container.ReleaseName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(release => lowerReleaseNamesOfPage.IndexOf(release.Key.ToLowerInvariant()))
            .Select(release =>
                CreateReleaseReadModel(
                    releaseName: release.First().ReleaseName,
                    containers: release.Select(container => container.ReadModel).ToList()
                )
            )
            .ToList();

        return new PagedResult<LinkCrypterImportReleaseReadModel>(
            Items: releases,
            TotalCount: totalCount,
            PageIndex: pageIndex,
            PageSize: pageSize
        );
    }

    private static LinkCrypterImportReleaseReadModel CreateReleaseReadModel(
        string releaseName,
        List<LinkCrypterImportContainerReadModel> containers
    )
    {
        var previewWithReleaseAction = containers
            .Select(container => container.Preview)
            .FirstOrDefault(preview => preview.ReleaseAction is not null);
        var importableContainers = containers
            .Where(container => container.Status is LinkCrypterImportContainerStatus.Importable)
            .ToList();

        return new LinkCrypterImportReleaseReadModel(
            ReleaseName: releaseName,
            ReleaseAction: previewWithReleaseAction?.ReleaseAction,
            ExistingReleaseId: previewWithReleaseAction?.ExistingReleaseId,
            ReleaseFolderPath: previewWithReleaseAction?.ReleaseFolderPath,
            ArchiveFolderPath: previewWithReleaseAction?.ArchiveFolderPath,
            IsSelected: importableContainers.Count > 0
                && importableContainers.All(container => container.IsSelected),
            HasImportableContainers: importableContainers.Count > 0,
            Containers: containers
        );
    }

    private async Task<
        IReadOnlyList<LinkCrypterImportHosterRegistrationReadModel>
    > GetHosterRegistrationsAsync(int linkCrypterImportId, CancellationToken cancellationToken)
    {
        var choices = await dbRead
            .LinkCrypterImportHosterRegistrations.Where(choice =>
                choice.LinkCrypterImportId == linkCrypterImportId
            )
            .OrderBy(choice => choice.HosterClassName)
            .Select(choice => new { choice.HosterClassName, choice.HosterRegistrationId })
            .ToListAsync(cancellationToken);
        var hosterClassNames = choices.Select(choice => choice.HosterClassName).ToList();
        var optionsByHosterClassName = (
            await dbRead
                .HosterRegistrations.Where(registration =>
                    hosterClassNames.Contains(registration.HosterClassName)
                )
                .OrderBy(registration => registration.Name)
                .Select(registration => new
                {
                    registration.HosterClassName,
                    Option = new LinkCrypterImportHosterRegistrationOptionReadModel(
                        Id: registration.Id,
                        Name: registration.Name,
                        IsActive: registration.IsActive
                    ),
                })
                .ToListAsync(cancellationToken)
        ).ToLookup(entry => entry.HosterClassName, entry => entry.Option);

        return choices
            .Select(choice => new LinkCrypterImportHosterRegistrationReadModel(
                HosterClassName: choice.HosterClassName,
                HosterRegistrationId: choice.HosterRegistrationId,
                Options: optionsByHosterClassName[choice.HosterClassName].ToList()
            ))
            .ToList();
    }
}
