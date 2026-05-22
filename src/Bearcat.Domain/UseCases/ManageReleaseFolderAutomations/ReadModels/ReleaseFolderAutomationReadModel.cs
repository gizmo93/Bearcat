namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;

public record ReleaseFolderAutomationReadModel(
    int ReleaseFolderAutomationId,
    string BasePath,
    string? FolderNamePattern,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    bool IsEnabled
);
