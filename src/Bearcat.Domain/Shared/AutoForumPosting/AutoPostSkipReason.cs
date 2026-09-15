namespace Bearcat.Domain.Shared.AutoForumPosting;

public enum AutoPostSkipReason
{
    Blocked = 1,
    SiteNotConfigured = 2,
    AlreadyPosted = 3,
    NoMatch = 4,
}
