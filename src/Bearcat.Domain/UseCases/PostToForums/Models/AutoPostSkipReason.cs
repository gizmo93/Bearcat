namespace Bearcat.Domain.UseCases.PostToForums.Models;

public enum AutoPostSkipReason
{
    Blocked = 1,
    SiteNotConfigured = 2,
    AlreadyPosted = 3,
    NoMatch = 4,
}
