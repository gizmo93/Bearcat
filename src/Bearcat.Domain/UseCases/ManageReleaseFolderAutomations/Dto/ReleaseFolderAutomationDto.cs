namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Dto;

public record ReleaseFolderAutomationDto(
    int ReleaseFolderAutomationId,
    string BasePath,
    string? FolderNamePattern,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    bool IsEnabled
);
