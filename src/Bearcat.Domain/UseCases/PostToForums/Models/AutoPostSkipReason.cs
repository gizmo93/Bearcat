namespace Bearcat.Domain.UseCases.PostToForums.Models;

public enum AutoPostSkipReason
{
    Blocked = 1,
    SiteNotConfigured = 2,
    AlreadyPosted = 3,
    NotPosted = 4,
    NoMatch = 5,
    ServiceRestrictionNotMet = 6,
}
