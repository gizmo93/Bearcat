namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Deletion;

public record AdditionalArchiveContentDeleteResult(
    bool IsDeleted,
    IReadOnlyList<string> UsingReleaseTemplateNames,
    IReadOnlyList<string> UsingReleaseNames
);
