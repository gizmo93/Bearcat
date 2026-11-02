using Bearcat.Abstractions;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Dto;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers;

public class LinkCrypterImportService(
    ILinkCrypterImportRepository repository,
    ILinkCrypterFactory linkCrypterFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider
)
{
    public async Task<int> StartAsync(
        int linkCrypterRegistrationId,
        int releaseTemplateId,
        string? archiveFilesBasePathForReleasesWithoutLocalFiles,
        CancellationToken cancellationToken = default
    )
    {
        var registration =
            await repository.GetLinkCrypterRegistrationOrDefaultAsync(
                linkCrypterRegistrationId,
                cancellationToken
            )
            ?? throw new InvalidLinkCrypterImportException(
                $"Link crypter registration {linkCrypterRegistrationId} was not found."
            );

        if (!registration.IsActive)
        {
            throw new InvalidLinkCrypterImportException(
                $"Link crypter registration {registration.Name} is not active."
            );
        }

        if (
            linkCrypterFactory.Get(registration.LinkCrypterClassName)
            is not ILinkCrypterWithContainerReading
        )
        {
            throw new InvalidLinkCrypterImportException(
                $"The link crypter of registration {registration.Name} cannot read existing containers."
            );
        }

        if (!await repository.ReleaseTemplateExistsAsync(releaseTemplateId, cancellationToken))
        {
            throw new InvalidLinkCrypterImportException(
                $"Release template {releaseTemplateId} was not found."
            );
        }

        var archiveFilesBasePath = archiveFilesBasePathForReleasesWithoutLocalFiles
            .TrimOrNullWhenWhitespace()
            ?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (
            archiveFilesBasePath is not null
            && !fileSystemService.DirectoryExists(archiveFilesBasePath)
        )
        {
            throw new InvalidLinkCrypterImportException(
                $"The archive folder {archiveFilesBasePath} does not exist."
            );
        }

        var localNow = timeProvider.GetLocalNow();

        var linkCrypterImport = new LinkCrypterImport
        {
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ReleaseTemplateId = releaseTemplateId,
            ArchiveFilesBasePathForReleasesWithoutLocalFiles = archiveFilesBasePath,
            State = LinkCrypterImportState.ReadingContainers,
            CreatedAt = localNow,
            UpdatedAt = localNow,
            ErrorMessages = [],
        };

        repository.Add(linkCrypterImport);
        await repository.SaveChangesAsync(cancellationToken);

        return linkCrypterImport.Id;
    }

    public async Task SetHosterRegistrationsAsync(
        int linkCrypterImportId,
        IReadOnlyList<LinkCrypterImportHosterRegistrationChoice> choices,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterImport = await repository.GetByIdAsync(
            linkCrypterImportId,
            cancellationToken
        );

        EnsureState(linkCrypterImport, LinkCrypterImportState.WaitingForHosterRegistrations);

        var hosterRegistrationsById = (
            await repository.GetHosterRegistrationsAsync(
                choices.Select(choice => choice.HosterRegistrationId).Distinct().ToList(),
                cancellationToken
            )
        ).ToDictionary(registration => registration.Id);

        foreach (var hosterRegistrationChoice in linkCrypterImport.HosterRegistrations)
        {
            var choice =
                choices.FirstOrDefault(choice =>
                    choice.HosterClassName == hosterRegistrationChoice.HosterClassName
                )
                ?? throw new InvalidLinkCrypterImportException(
                    $"A hoster account must be chosen for hoster {hosterRegistrationChoice.HosterClassName}."
                );

            if (
                !hosterRegistrationsById.TryGetValue(
                    choice.HosterRegistrationId,
                    out var hosterRegistration
                )
                || hosterRegistration.HosterClassName != hosterRegistrationChoice.HosterClassName
            )
            {
                throw new InvalidLinkCrypterImportException(
                    $"Hoster account {choice.HosterRegistrationId} does not belong to hoster {hosterRegistrationChoice.HosterClassName}."
                );
            }

            hosterRegistrationChoice.HosterRegistrationId = hosterRegistration.Id;
        }

        linkCrypterImport.ChangeState(
            LinkCrypterImportState.CheckingFiles,
            timeProvider.GetLocalNow()
        );

        linkCrypterImport.ErrorMessages = [];
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SetContainerSelectionAsync(
        int linkCrypterImportId,
        IReadOnlyList<int> containerIds,
        bool isSelected,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterImport = await repository.GetWithContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        EnsureState(linkCrypterImport, LinkCrypterImportState.ReadyForReview);

        var selectedContainerIds = containerIds.ToHashSet();

        foreach (
            var container in GetImportableContainers(linkCrypterImport)
                .Where(container => selectedContainerIds.Contains(container.Id))
        )
        {
            container.IsSelected = isSelected;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SetSelectionOfAllImportableContainersAsync(
        int linkCrypterImportId,
        bool isSelected,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterImport = await repository.GetWithContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        EnsureState(linkCrypterImport, LinkCrypterImportState.ReadyForReview);

        foreach (var container in GetImportableContainers(linkCrypterImport))
        {
            container.IsSelected = isSelected;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task StartImportAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterImport = await repository.GetWithContainersAsync(
            linkCrypterImportId,
            cancellationToken
        );

        EnsureState(linkCrypterImport, LinkCrypterImportState.ReadyForReview);

        linkCrypterImport.ChangeState(LinkCrypterImportState.Importing, timeProvider.GetLocalNow());

        linkCrypterImport.StartProgress(
            GetImportableContainers(linkCrypterImport).Count(container => container.IsSelected)
        );

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int linkCrypterImportId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterImport = await repository.GetByIdAsync(
            linkCrypterImportId,
            cancellationToken
        );

        if (linkCrypterImport.State is LinkCrypterImportState.Importing)
        {
            throw new InvalidLinkCrypterImportException(
                "A link crypter import cannot be deleted while its containers are imported."
            );
        }

        repository.Remove(linkCrypterImport);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static List<LinkCrypterImportContainer> GetImportableContainers(
        LinkCrypterImport linkCrypterImport
    )
    {
        return linkCrypterImport
            .Containers.Where(container =>
                container.Status is LinkCrypterImportContainerStatus.Importable
            )
            .ToList();
    }

    private static void EnsureState(
        LinkCrypterImport linkCrypterImport,
        LinkCrypterImportState expectedState
    )
    {
        if (linkCrypterImport.State != expectedState)
        {
            throw new InvalidLinkCrypterImportException(
                $"Link crypter import {linkCrypterImport.Id} must be in state {expectedState} but is in state {linkCrypterImport.State}."
            );
        }
    }
}
