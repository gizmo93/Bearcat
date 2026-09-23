using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class PageNavigation : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int CurrentPage { get; set; }

    [Parameter]
    [EditorRequired]
    public int TotalPages { get; set; }

    [Parameter]
    public EventCallback<int> OnPageSelected { get; set; }

    private IReadOnlyList<int?> PageItems
    {
        get
        {
            if (TotalPages <= 7)
            {
                return Enumerable.Range(1, TotalPages).Select(page => (int?)page).ToList();
            }

            var pages = new List<int?> { 1 };
            var start = Math.Max(2, CurrentPage - 1);
            var end = Math.Min(TotalPages - 1, CurrentPage + 1);

            if (start > 2)
            {
                pages.Add(null);
            }

            pages.AddRange(Enumerable.Range(start, end - start + 1).Select(page => (int?)page));

            if (end < TotalPages - 1)
            {
                pages.Add(null);
            }

            pages.Add(TotalPages);
            return pages;
        }
    }

    private async Task SelectPageAsync(int page)
    {
        var clampedPage = Math.Clamp(page, 1, TotalPages);

        if (clampedPage != CurrentPage)
        {
            await OnPageSelected.InvokeAsync(clampedPage);
        }
    }
}
