using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;

public record ReleaseFolderAutomationReadModel(
    int ReleaseFolderAutomationId,
    string BasePath,
    string? FolderNamePattern,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    ReleaseType ReleaseType,
    ReleaseContentType ReleaseContentType,
    bool IsEnabled
);
