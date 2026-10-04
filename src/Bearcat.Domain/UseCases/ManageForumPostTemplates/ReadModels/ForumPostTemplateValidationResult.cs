namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

public record ForumPostTemplateValidationResult(bool IsValid, IReadOnlyList<string> Errors);
