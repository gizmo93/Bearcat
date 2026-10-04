using System.Globalization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class SearchResultsPagination : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int TotalCount { get; set; }

    [Parameter]
    [EditorRequired]
    public int PageIndex { get; set; }

    [Parameter]
    [EditorRequired]
    public int PageSize { get; set; }

    [Parameter]
    [EditorRequired]
    public string ResultsSummaryResourceKey { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public Func<int, string> GetPageUrl { get; set; } = null!;

    [Parameter]
    public EventCallback<int> PageSizeChanged { get; set; }

    private int CurrentPage => TotalCount == 0 ? 1 : PageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    private int FirstResult => TotalCount == 0 ? 0 : PageIndex * PageSize + 1;
    private int LastResult => Math.Min(TotalCount, (PageIndex + 1) * PageSize);

    private static IReadOnlyList<SelectOption<int>> PageSizeOptions =>
        SearchUrlParameters
            .PageSizes.Select(size => new SelectOption<int>(
                size,
                size.ToString(CultureInfo.CurrentCulture)
            ))
            .ToList();
}
