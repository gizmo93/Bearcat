namespace Bearcat.Website.Layout.Navigation;

public sealed record NavigationSection(
    string Id,
    string HeadingLabelKey,
    NavigationSectionHeadingKind HeadingKind,
    IReadOnlyList<NavigationItem> Items
);
