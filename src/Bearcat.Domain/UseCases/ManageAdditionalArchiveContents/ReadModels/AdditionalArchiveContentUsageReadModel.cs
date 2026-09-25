namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;

public record AdditionalArchiveContentUsageReadModel(
    IReadOnlyList<string> ReleaseTemplateNames,
    IReadOnlyList<string> ReleaseNames
)
{
    public bool IsUsed => ReleaseTemplateNames.Count > 0 || ReleaseNames.Count > 0;
}
