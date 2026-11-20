using Bearcat.Domain.Entities;
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
                SeriesReleaseTemplateName: import.SeriesReleaseTemplate != null
                    ? import.SeriesReleaseTemplate.Name
                    : null,
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
                import.SeriesReleaseTemplateId,
                SeriesReleaseTemplateName = import.SeriesReleaseTemplate != null
                    ? import.SeriesReleaseTemplate.Name
                    : null,
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
        var releaseNamesOfSelectedImportableContainers =
            import.State is LinkCrypterImportState.ReadyForReview
                ? await dbRead
                    .LinkCrypterImportContainers.Where(container =>
                        container.LinkCrypterImportId == linkCrypterImportId
                        && container.IsSelected
                        && container.Status == LinkCrypterImportContainerStatus.Importable
                    )
                    .Select(container => container.ReleaseName)
                    .ToListAsync(cancellationToken)
                : [];

        return new LinkCrypterImportDetailReadModel(
            Id: import.Id,
            LinkCrypterRegistrationId: import.LinkCrypterRegistrationId,
            LinkCrypterRegistrationName: import.LinkCrypterRegistrationName,
            ReleaseTemplateId: import.ReleaseTemplateId,
            ReleaseTemplateName: import.ReleaseTemplateName,
            SeriesReleaseTemplateId: import.SeriesReleaseTemplateId,
            SeriesReleaseTemplateName: import.SeriesReleaseTemplateName,
            ArchiveFilesBasePathForReleasesWithoutLocalFiles: import.ArchiveFilesBasePathForReleasesWithoutLocalFiles,
            PrimaryLanguageCode: import.PrimaryLanguageCode,
            State: import.State,
            CreatedAt: import.CreatedAt,
            UpdatedAt: import.UpdatedAt,
            ProcessedContainerCount: import.ProcessedContainerCount,
            ContainerCountToProcess: import.ContainerCountToProcess,
            SelectedImportableContainerCount: releaseNamesOfSelectedImportableContainers.Count,
            ReleaseCountWithSelectedImportableContainers: releaseNamesOfSelectedImportableContainers
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
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
        var importState = await dbRead
            .LinkCrypterImports.Where(import => import.Id == query.Filter.LinkCrypterImportId)
            .Select(import => import.State)
            .FirstAsync(cancellationToken);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);
        var containersOfImport = dbRead.LinkCrypterImportContainers.Where(container =>
            container.LinkCrypterImportId == query.Filter.LinkCrypterImportId
        );
        var matchingContainers = GetMatchingContainers(query.Filter);

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
                container.ReleaseTemplateId,
                ReleaseTemplateName = container.ReleaseTemplate.Name,
                container.PrimaryLanguageCode,
                container.ContainerUrl,
                container.HasBackupLinks,
                container.IsSelected,
                container.Status,
                container.NotImportableReason,
                container.ImportedReleaseId,
                container.ImportedReleaseCollectionId,
                container.ErrorMessages,
                container.PreviewJson,
            })
            .ToListAsync(cancellationToken);

        var releases = containers
            .Select(container => new ContainerOfRelease(
                ReleaseName: container.ReleaseName,
                ReleaseTemplateId: container.ReleaseTemplateId,
                ReleaseTemplateName: container.ReleaseTemplateName,
                PrimaryLanguageCode: container.PrimaryLanguageCode,
                ReadModel: new LinkCrypterImportContainerReadModel(
                    Id: container.Id,
                    ContainerName: container.ContainerName,
                    ContainerUrl: container.ContainerUrl,
                    HasBackupLinks: container.HasBackupLinks,
                    IsSelected: container.IsSelected,
                    IsSelectable: IsSelectable(importState, container.Status),
                    Status: container.Status,
                    NotImportableReason: container.NotImportableReason,
                    ImportedReleaseId: container.ImportedReleaseId,
                    ImportedReleaseCollectionId: container.ImportedReleaseCollectionId,
                    ErrorMessages: container.ErrorMessages,
                    Preview: LinkCrypterImportContainerPreview.FromJson(container.PreviewJson)
                )
            ))
            .GroupBy(container => container.ReleaseName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(release => lowerReleaseNamesOfPage.IndexOf(release.Key.ToLowerInvariant()))
            .Select(release =>
                CreateReleaseReadModel(
                    containerWithReleaseSettings: release.FirstOrDefault(container =>
                        container.ReadModel.Status
                            is LinkCrypterImportContainerStatus.Importable
                                or LinkCrypterImportContainerStatus.NotImportable
                    ) ?? release.First(),
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

    public async Task<
        IReadOnlyList<LinkCrypterImportSelectableContainerReadModel>
    > GetSelectableContainersOfFilteredReleasesAsync(
        LinkCrypterImportReleaseFilter filter,
        CancellationToken cancellationToken = default
    )
    {
        var lowerReleaseNamesOfFilteredReleases = GetMatchingContainers(filter)
            .Select(container => container.ReleaseName.ToLower());

        return await dbRead
            .LinkCrypterImportContainers.Where(container =>
                container.LinkCrypterImportId == filter.LinkCrypterImportId
                && container.LinkCrypterImport.State == LinkCrypterImportState.ReadyForReview
                && (
                    container.Status == LinkCrypterImportContainerStatus.Importable
                    || container.Status == LinkCrypterImportContainerStatus.NotImportable
                )
                && lowerReleaseNamesOfFilteredReleases.Contains(container.ReleaseName.ToLower())
            )
            .OrderBy(container => container.Id)
            .Select(container => new LinkCrypterImportSelectableContainerReadModel(
                container.Id,
                container.ReleaseName,
                container.IsSelected
            ))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<LinkCrypterImportContainer> GetMatchingContainers(
        LinkCrypterImportReleaseFilter filter
    )
    {
        var matchingContainers = dbRead.LinkCrypterImportContainers.Where(container =>
            container.LinkCrypterImportId == filter.LinkCrypterImportId
        );

        if (filter.Statuses.Count > 0)
        {
            matchingContainers = matchingContainers.Where(container =>
                EF.Parameter(filter.Statuses).Contains(container.Status)
            );
        }

        var searchText = SearchTextPatterns.TrimOrNullWhenEmpty(filter.SearchText);

        if (searchText is not null)
        {
            var pattern = SearchTextPatterns.ToLowerCaseWildcardPattern(searchText);

            matchingContainers = matchingContainers.Where(container =>
                EF.Functions.Like(
                    container.ReleaseName.ToLower(),
                    pattern,
                    SearchTextPatterns.LikeEscapeCharacter
                )
                || EF.Functions.Like(
                    container.ContainerName.ToLower(),
                    pattern,
                    SearchTextPatterns.LikeEscapeCharacter
                )
            );
        }

        return matchingContainers;
    }

    private static bool IsSelectable(
        LinkCrypterImportState importState,
        LinkCrypterImportContainerStatus status
    )
    {
        return importState is LinkCrypterImportState.ReadyForReview
            && status
                is LinkCrypterImportContainerStatus.Importable
                    or LinkCrypterImportContainerStatus.NotImportable;
    }

    private static LinkCrypterImportReleaseReadModel CreateReleaseReadModel(
        ContainerOfRelease containerWithReleaseSettings,
        List<LinkCrypterImportContainerReadModel> containers
    )
    {
        var releasePreview = containers
            .Select(container => container.Preview.Releases)
            .FirstOrDefault(releasePreviews => releasePreviews?.Count == 1)
            ?.Single();
        var selectableContainers = containers.Where(container => container.IsSelectable).ToList();

        return new LinkCrypterImportReleaseReadModel(
            ReleaseName: containerWithReleaseSettings.ReleaseName,
            ReleaseTemplateId: containerWithReleaseSettings.ReleaseTemplateId,
            ReleaseTemplateName: containerWithReleaseSettings.ReleaseTemplateName,
            PrimaryLanguageCode: containerWithReleaseSettings.PrimaryLanguageCode,
            ReleaseAction: releasePreview?.ReleaseAction,
            ExistingReleaseId: releasePreview?.ExistingReleaseId,
            ReleaseFolderPath: releasePreview?.ReleaseFolderPath,
            ArchiveFolderPath: releasePreview?.ArchiveFolderPath,
            IsSelected: selectableContainers.Count > 0
                && selectableContainers.All(container => container.IsSelected),
            HasSelectableContainers: selectableContainers.Count > 0,
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

    private sealed record ContainerOfRelease(
        string ReleaseName,
        int ReleaseTemplateId,
        string ReleaseTemplateName,
        string? PrimaryLanguageCode,
        LinkCrypterImportContainerReadModel ReadModel
    );
}
