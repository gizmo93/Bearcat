namespace Bearcat.Domain.UseCases.PostToForums;

public enum AutoPostSkipReason
{
    Blocked = 1,
    SiteNotConfigured = 2,
    AlreadyPosted = 3,
    NoMatch = 4,
}
