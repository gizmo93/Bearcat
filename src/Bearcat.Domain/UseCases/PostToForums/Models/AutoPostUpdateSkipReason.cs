namespace Bearcat.Domain.UseCases.PostToForums.Models;

public enum AutoPostUpdateSkipReason
{
    Blocked = 1,
    SiteNotConfigured = 2,
    NotPosted = 3,
}
