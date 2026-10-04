namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

public record ForumPostTemplateError(string Message, int? Line, int? Column);
