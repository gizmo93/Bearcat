using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Deletion;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Dto;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Repositories;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Validation;
using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.Options;

namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders;

public class ArchiveStorageFolderService(
    IArchiveStorageFolderReadRepository readRepository,
    IArchiveStorageFolderWriteRepository writeRepository,
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
)
{
    private const int NameMaxLength = 100;
    private const int PathMaxLength = 500;

    public async Task<
        IReadOnlyList<ArchiveStorageFolderWithAvailableFreeSpaceReadModel>
    > GetAllWithAvailableFreeSpaceAsync(CancellationToken cancellationToken = default)
    {
        var storageFolders = await readRepository.GetAllAsync(cancellationToken);

        return storageFolders
            .Select(storageFolder => new ArchiveStorageFolderWithAvailableFreeSpaceReadModel(
                storageFolder,
                fileSystemService.GetAvailableFreeSpaceBytes(storageFolder.Path)
            ))
            .ToList();
    }

    public async Task<ArchiveStorageFolderSaveResult> CreateAsync(
        ArchiveStorageFolderInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var validationErrors = await ValidateAsync(
            normalizedInput,
            existingStorageFolder: null,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return ArchiveStorageFolderSaveResult.Invalid(validationErrors);
        }

        var storageFolder = new ArchiveStorageFolder
        {
            Name = normalizedInput.Name,
            Path = normalizedInput.Path,
            IsActive = true,
        };
        ApplyInput(storageFolder, normalizedInput);

        writeRepository.Add(storageFolder);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return ArchiveStorageFolderSaveResult.Saved(storageFolder.Id);
    }

    public async Task<ArchiveStorageFolderSaveResult> UpdateAsync(
        int archiveStorageFolderId,
        ArchiveStorageFolderInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var storageFolder = await writeRepository.GetByIdAsync(
            archiveStorageFolderId,
            cancellationToken
        );
        var validationErrors = await ValidateAsync(
            normalizedInput,
            storageFolder,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return ArchiveStorageFolderSaveResult.Invalid(validationErrors);
        }

        ApplyInput(storageFolder, normalizedInput);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return ArchiveStorageFolderSaveResult.Saved(archiveStorageFolderId);
    }

    public async Task ToggleIsActiveAsync(
        int archiveStorageFolderId,
        CancellationToken cancellationToken = default
    )
    {
        var storageFolder = await writeRepository.GetByIdAsync(
            archiveStorageFolderId,
            cancellationToken
        );
        storageFolder.IsActive = !storageFolder.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArchiveStorageFolderDeleteResult> DeleteAsync(
        int archiveStorageFolderId,
        CancellationToken cancellationToken = default
    )
    {
        var storedArchiveCount = await writeRepository.GetStoredArchiveCountAsync(
            archiveStorageFolderId,
            cancellationToken
        );

        if (storedArchiveCount > 0)
        {
            return new ArchiveStorageFolderDeleteResult(IsDeleted: false, storedArchiveCount);
        }

        var storageFolder = await writeRepository.GetByIdAsync(
            archiveStorageFolderId,
            cancellationToken
        );
        writeRepository.Remove(storageFolder);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return new ArchiveStorageFolderDeleteResult(IsDeleted: true, StoredArchiveCount: 0);
    }

    private static ArchiveStorageFolderInput Normalize(ArchiveStorageFolderInput input)
    {
        var trimmedPath = input.Path.Trim();

        return input with
        {
            Name = input.Name.Trim(),
            Path = Path.IsPathFullyQualified(trimmedPath)
                ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmedPath))
                : trimmedPath,
        };
    }

    private static void ApplyInput(
        ArchiveStorageFolder storageFolder,
        ArchiveStorageFolderInput normalizedInput
    )
    {
        storageFolder.Name = normalizedInput.Name;
        storageFolder.Path = normalizedInput.Path;
        storageFolder.MinimumFreeSpaceGb = normalizedInput.MinimumFreeSpaceGb;
        storageFolder.Priority = normalizedInput.Priority;
        storageFolder.RetrieveArchivesBeforeReupload =
            normalizedInput.RetrieveArchivesBeforeReupload;
    }

    private async Task<List<ArchiveStorageFolderValidationError>> ValidateAsync(
        ArchiveStorageFolderInput normalizedInput,
        ArchiveStorageFolder? existingStorageFolder,
        CancellationToken cancellationToken
    )
    {
        var validationErrors = new List<ArchiveStorageFolderValidationError>();

        var nameError = await ValidateNameAsync(
            normalizedInput.Name,
            existingStorageFolder?.Id,
            cancellationToken
        );
        if (nameError is not null)
        {
            validationErrors.Add(nameError.Value);
        }

        var pathError = await ValidatePathAsync(
            normalizedInput.Path,
            existingStorageFolder,
            cancellationToken
        );
        if (pathError is not null)
        {
            validationErrors.Add(pathError.Value);
        }

        if (normalizedInput.MinimumFreeSpaceGb < 0)
        {
            validationErrors.Add(ArchiveStorageFolderValidationError.MinimumFreeSpaceNegative);
        }

        return validationErrors;
    }

    private async Task<ArchiveStorageFolderValidationError?> ValidateNameAsync(
        string name,
        int? excludedArchiveStorageFolderId,
        CancellationToken cancellationToken
    )
    {
        if (name.Length == 0)
        {
            return ArchiveStorageFolderValidationError.NameRequired;
        }

        if (name.Length > NameMaxLength)
        {
            return ArchiveStorageFolderValidationError.NameTooLong;
        }

        if (
            await writeRepository.NameExistsAsync(
                name,
                excludedArchiveStorageFolderId,
                cancellationToken
            )
        )
        {
            return ArchiveStorageFolderValidationError.NameAlreadyExists;
        }

        return null;
    }

    private async Task<ArchiveStorageFolderValidationError?> ValidatePathAsync(
        string path,
        ArchiveStorageFolder? existingStorageFolder,
        CancellationToken cancellationToken
    )
    {
        if (path.Length == 0)
        {
            return ArchiveStorageFolderValidationError.PathRequired;
        }

        if (path.Length > PathMaxLength)
        {
            return ArchiveStorageFolderValidationError.PathTooLong;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return ArchiveStorageFolderValidationError.PathNotAbsolute;
        }

        if (
            existingStorageFolder is not null
            && !string.Equals(existingStorageFolder.Path, path, StringComparison.Ordinal)
            && await writeRepository.GetStoredArchiveCountAsync(
                existingStorageFolder.Id,
                cancellationToken
            ) > 0
        )
        {
            return ArchiveStorageFolderValidationError.PathChangeWithStoredArchives;
        }

        if (OverlapsWorkingDirectory(path))
        {
            return ArchiveStorageFolderValidationError.PathOverlapsWorkingDirectory;
        }

        if (!fileSystemService.DirectoryExists(path))
        {
            return ArchiveStorageFolderValidationError.PathNotFound;
        }

        if (
            await writeRepository.PathExistsAsync(
                path,
                existingStorageFolder?.Id,
                cancellationToken
            )
        )
        {
            return ArchiveStorageFolderValidationError.PathAlreadyExists;
        }

        return null;
    }

    private bool OverlapsWorkingDirectory(string path)
    {
        return workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .Any(workingDirectory =>
                FolderPathHelper.IsSameOrSubPath(path, workingDirectory)
                || FolderPathHelper.IsSameOrSubPath(workingDirectory, path)
            );
    }
}
