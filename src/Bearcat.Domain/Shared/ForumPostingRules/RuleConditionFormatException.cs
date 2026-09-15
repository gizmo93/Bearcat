namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed class RuleConditionFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);
