using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class PageNavigation : ComponentBase
{
    private const int MaximumPageCountWithoutGaps = 7;

    [Parameter]
    [EditorRequired]
    public int CurrentPage { get; set; }

    [Parameter]
    [EditorRequired]
    public int TotalPages { get; set; }

    [Parameter]
    public EventCallback<int> OnPageSelected { get; set; }

    [Parameter]
    public Func<int, string>? GetPageUrl { get; set; }

    private IReadOnlyList<PageLink> PageLinksWithGaps
    {
        get
        {
            if (TotalPages <= MaximumPageCountWithoutGaps)
            {
                return Enumerable.Range(1, TotalPages).Select(PageLink.ForPage).ToList();
            }

            var pageLinks = new List<PageLink> { PageLink.ForPage(1) };
            var firstNeighbourPage = Math.Max(2, CurrentPage - 1);
            var lastNeighbourPage = Math.Min(TotalPages - 1, CurrentPage + 1);

            if (firstNeighbourPage > 2)
            {
                pageLinks.Add(PageLink.Gap);
            }

            pageLinks.AddRange(
                Enumerable
                    .Range(firstNeighbourPage, lastNeighbourPage - firstNeighbourPage + 1)
                    .Select(PageLink.ForPage)
            );

            if (lastNeighbourPage < TotalPages - 1)
            {
                pageLinks.Add(PageLink.Gap);
            }

            pageLinks.Add(PageLink.ForPage(TotalPages));
            return pageLinks;
        }
    }

    private string? GetPageUrlOrNull(int page)
    {
        return GetPageUrl?.Invoke(page);
    }

    private async Task SelectPageAsync(int page)
    {
        var clampedPage = Math.Clamp(page, 1, TotalPages);

        if (clampedPage != CurrentPage)
        {
            await OnPageSelected.InvokeAsync(clampedPage);
        }
    }

    private sealed record PageLink(int? PageNumber)
    {
        public static readonly PageLink Gap = new((int?)null);

        public static PageLink ForPage(int pageNumber) => new(pageNumber);
    }
}
