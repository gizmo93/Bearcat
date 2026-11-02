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

        return new LinkCrypterImportDetailReadModel(
            Id: import.Id,
            LinkCrypterRegistrationId: import.LinkCrypterRegistrationId,
            LinkCrypterRegistrationName: import.LinkCrypterRegistrationName,
            ReleaseTemplateId: import.ReleaseTemplateId,
            ReleaseTemplateName: import.ReleaseTemplateName,
            ArchiveFilesBasePathForReleasesWithoutLocalFiles: import.ArchiveFilesBasePathForReleasesWithoutLocalFiles,
            State: import.State,
            CreatedAt: import.CreatedAt,
            UpdatedAt: import.UpdatedAt,
            ProcessedContainerCount: import.ProcessedContainerCount,
            ContainerCountToProcess: import.ContainerCountToProcess,
            SelectedContainerCount: selectedContainerCount,
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

    public async Task<PagedResult<LinkCrypterImportContainerReadModel>> SearchContainersAsync(
        LinkCrypterImportContainerSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);
        var containersQuery = dbRead.LinkCrypterImportContainers.Where(container =>
            container.LinkCrypterImportId == query.LinkCrypterImportId
        );

        if (query.Statuses.Count > 0)
        {
            containersQuery = containersQuery.Where(container =>
                EF.Parameter(query.Statuses).Contains(container.Status)
            );
        }

        var totalCount = await containersQuery.CountAsync(cancellationToken);
        var containers = await containersQuery
            .OrderBy(container => container.ContainerName)
            .ThenBy(container => container.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(container => new
            {
                container.Id,
                container.ContainerName,
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

        return new PagedResult<LinkCrypterImportContainerReadModel>(
            Items: containers
                .Select(container => new LinkCrypterImportContainerReadModel(
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
                ))
                .ToList(),
            TotalCount: totalCount,
            PageIndex: pageIndex,
            PageSize: pageSize
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
