namespace Bearcat.Domain.Shared.ForumPosting;

public static class ForumServiceRestrictionFilter
{
    public static (
        List<T> AllowedItems,
        string? BlockedReason
    ) RemoveForbiddenItemsAndFindBlockedReason<T>(
        IReadOnlyList<T> items,
        Func<T, string> getClassName,
        IReadOnlyList<string> requiredClassNames,
        IReadOnlyList<string> forbiddenClassNames,
        string kindName
    )
    {
        var allowedItems = items
            .Where(item => !forbiddenClassNames.Contains(getClassName(item)))
            .ToList();

        if (items.Count > 0 && allowedItems.Count == 0)
        {
            return (allowedItems, $"No allowed {kindName} is left.");
        }

        if (
            requiredClassNames.Count > 0
            && !allowedItems.Any(item => requiredClassNames.Contains(getClassName(item)))
        )
        {
            return (
                allowedItems,
                $"None of the required {kindName}s is present: {string.Join(", ", requiredClassNames)}."
            );
        }

        return (allowedItems, null);
    }
}
