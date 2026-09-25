using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Deletion;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Dto;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Repositories;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Validation;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents;

public class AdditionalArchiveContentService(
    IAdditionalArchiveContentWriteRepository writeRepository,
    IFileSystemService fileSystemService
)
{
    private const int NameMaxLength = 200;
    private const int SourcePathMaxLength = 1000;
    private const int FileNameMaxLength = 255;

    public async Task<AdditionalArchiveContentSaveResult> CreateAsync(
        AdditionalArchiveContentInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var validationErrors = await ValidateAsync(
            normalizedInput,
            excludedAdditionalArchiveContentId: null,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return AdditionalArchiveContentSaveResult.Invalid(validationErrors);
        }

        var additionalArchiveContent = new AdditionalArchiveContent();
        ApplyInput(additionalArchiveContent, normalizedInput);

        writeRepository.Add(additionalArchiveContent);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return AdditionalArchiveContentSaveResult.Saved(additionalArchiveContent.Id);
    }

    public async Task<AdditionalArchiveContentSaveResult> UpdateAsync(
        int additionalArchiveContentId,
        AdditionalArchiveContentInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedInput = Normalize(input);
        var validationErrors = await ValidateAsync(
            normalizedInput,
            additionalArchiveContentId,
            cancellationToken
        );

        if (validationErrors.Count > 0)
        {
            return AdditionalArchiveContentSaveResult.Invalid(validationErrors);
        }

        var additionalArchiveContent = await writeRepository.GetByIdAsync(
            additionalArchiveContentId,
            cancellationToken
        );
        ApplyInput(additionalArchiveContent, normalizedInput);

        await writeRepository.SaveChangesAsync(cancellationToken);

        return AdditionalArchiveContentSaveResult.Saved(additionalArchiveContentId);
    }

    public async Task<AdditionalArchiveContentDeleteResult> DeleteAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken = default
    )
    {
        var usage = await writeRepository.GetUsageAsync(
            additionalArchiveContentId,
            cancellationToken
        );

        if (usage.IsUsed)
        {
            return new AdditionalArchiveContentDeleteResult(
                IsDeleted: false,
                usage.ReleaseTemplateNames,
                usage.ReleaseNames
            );
        }

        var additionalArchiveContent = await writeRepository.GetByIdAsync(
            additionalArchiveContentId,
            cancellationToken
        );
        writeRepository.Remove(additionalArchiveContent);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return new AdditionalArchiveContentDeleteResult(IsDeleted: true, [], []);
    }

    private static AdditionalArchiveContentInput Normalize(AdditionalArchiveContentInput input)
    {
        return input.Type == AdditionalArchiveContentType.Path
            ? new AdditionalArchiveContentInput(
                input.Name.Trim(),
                input.Type,
                input.SourcePath?.Trim(),
                FileName: null,
                TextContent: null
            )
            : new AdditionalArchiveContentInput(
                input.Name.Trim(),
                input.Type,
                SourcePath: null,
                input.FileName?.Trim(),
                input.TextContent
            );
    }

    private static void ApplyInput(
        AdditionalArchiveContent additionalArchiveContent,
        AdditionalArchiveContentInput normalizedInput
    )
    {
        additionalArchiveContent.Name = normalizedInput.Name;
        additionalArchiveContent.Type = normalizedInput.Type;
        additionalArchiveContent.SourcePath = normalizedInput.SourcePath;
        additionalArchiveContent.FileName = normalizedInput.FileName;
        additionalArchiveContent.TextContent = normalizedInput.TextContent;
    }

    private async Task<List<AdditionalArchiveContentValidationError>> ValidateAsync(
        AdditionalArchiveContentInput normalizedInput,
        int? excludedAdditionalArchiveContentId,
        CancellationToken cancellationToken
    )
    {
        var validationErrors = new List<AdditionalArchiveContentValidationError>();

        var nameError = await ValidateNameAsync(
            normalizedInput.Name,
            excludedAdditionalArchiveContentId,
            cancellationToken
        );
        if (nameError is not null)
        {
            validationErrors.Add(nameError.Value);
        }

        if (normalizedInput.Type == AdditionalArchiveContentType.Path)
        {
            var sourcePathError = ValidateSourcePath(normalizedInput.SourcePath);
            if (sourcePathError is not null)
            {
                validationErrors.Add(sourcePathError.Value);
            }
        }
        else
        {
            var fileNameError = ValidateFileName(normalizedInput.FileName);
            if (fileNameError is not null)
            {
                validationErrors.Add(fileNameError.Value);
            }

            if (string.IsNullOrWhiteSpace(normalizedInput.TextContent))
            {
                validationErrors.Add(AdditionalArchiveContentValidationError.TextContentRequired);
            }
        }

        return validationErrors;
    }

    private async Task<AdditionalArchiveContentValidationError?> ValidateNameAsync(
        string name,
        int? excludedAdditionalArchiveContentId,
        CancellationToken cancellationToken
    )
    {
        if (name.Length == 0)
        {
            return AdditionalArchiveContentValidationError.NameRequired;
        }

        if (name.Length > NameMaxLength)
        {
            return AdditionalArchiveContentValidationError.NameTooLong;
        }

        if (
            await writeRepository.NameExistsAsync(
                name,
                excludedAdditionalArchiveContentId,
                cancellationToken
            )
        )
        {
            return AdditionalArchiveContentValidationError.NameAlreadyExists;
        }

        return null;
    }

    private AdditionalArchiveContentValidationError? ValidateSourcePath(string? sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            return AdditionalArchiveContentValidationError.SourcePathRequired;
        }

        if (sourcePath.Length > SourcePathMaxLength)
        {
            return AdditionalArchiveContentValidationError.SourcePathTooLong;
        }

        if (
            !fileSystemService.FileExists(sourcePath)
            && !fileSystemService.DirectoryExists(sourcePath)
        )
        {
            return AdditionalArchiveContentValidationError.SourcePathNotFound;
        }

        return null;
    }

    private static AdditionalArchiveContentValidationError? ValidateFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return AdditionalArchiveContentValidationError.FileNameRequired;
        }

        if (fileName.Length > FileNameMaxLength)
        {
            return AdditionalArchiveContentValidationError.FileNameTooLong;
        }

        if (
            fileName is "." or ".."
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
        )
        {
            return AdditionalArchiveContentValidationError.FileNameInvalid;
        }

        if (
            string.Equals(
                fileName,
                ReleaseFolderEntriesForPackingService.NonceFileName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return AdditionalArchiveContentValidationError.FileNameReserved;
        }

        return null;
    }
}
