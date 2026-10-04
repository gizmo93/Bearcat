using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Scriban.Parsing;
using Scriban.Syntax;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;

internal static class ForumPostTemplateErrorMapper
{
    public static IReadOnlyList<ForumPostTemplateError> FromParserMessages(
        IEnumerable<LogMessage> messages
    )
    {
        return messages
            .Where(message => message.Type == ParserMessageType.Error)
            .Select(message => CreateError(message.Message, message.Span.Start))
            .ToList();
    }

    public static ForumPostTemplateError FromRuntimeException(ScriptRuntimeException exception)
    {
        return CreateError(exception.OriginalMessage, exception.Span.Start);
    }

    private static ForumPostTemplateError CreateError(string message, TextPosition position)
    {
        return position.Line < 0 || position.Column < 0
            ? new ForumPostTemplateError(message, Line: null, Column: null)
            : new ForumPostTemplateError(message, position.Line + 1, position.Column + 1);
    }
}
