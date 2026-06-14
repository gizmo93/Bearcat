using System.Globalization;
using Bearcat.Domain.UseCases.Dashboard.ReadModels;
using Bearcat.Domain.UseCases.Dashboard.Repositories;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.Dashboard;

public partial class DashboardPage(IDashboardReadRepository readRepository)
{
    private const int ChartColorCount = 20;
    private const string DateKey = "Date";

    private readonly List<ChartSeries> chartSeries = [];
    private IReadOnlyList<object> chartRows = [];
    private readonly List<ReleaseStatusChartRow> releaseStatusRows = [];
    private ChartConfig chartConfig = ChartConfig.Create();
    private DateRange? dateRange = GetCurrentWeekDateRange();
    private int totalReleaseCount;
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task OnDateRangeChangedAsync(DateRange? value)
    {
        dateRange = value;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            var uploadedFrom = dateRange is null
                ? (DateOnly?)null
                : DateOnly.FromDateTime(dateRange.Start);
            var uploadedTo = dateRange is null
                ? (DateOnly?)null
                : DateOnly.FromDateTime(dateRange.End);

            var uploads = await readRepository.GetUploadsPerDayAsync(
                uploadedFrom,
                uploadedTo,
                CancellationToken.None
            );
            var releaseSummary = await readRepository.GetReleaseOnlineStateSummaryAsync(
                CancellationToken.None
            );

            UpdateChart(uploads);
            UpdateReleaseStatusChart(releaseSummary);
        }
        finally
        {
            isLoading = false;
        }
    }

    private static DateRange GetCurrentWeekDateRange()
    {
        var today = DateTime.Today;
        var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = today.AddDays(-daysSinceMonday);
        var end = start.AddDays(6);

        return new DateRange(start, end);
    }

    private void UpdateChart(IReadOnlyList<UploadDayReadModel> uploads)
    {
        chartSeries.Clear();

        var hosterNames = uploads
            .Select(upload => upload.HosterName)
            .Distinct(StringComparer.CurrentCulture)
            .Order(StringComparer.CurrentCulture)
            .ToList();

        for (var index = 0; index < hosterNames.Count; index++)
        {
            chartSeries.Add(new ChartSeries($"Hoster{index}", hosterNames[index]));
        }

        var seriesKeyByHoster = chartSeries.ToDictionary(
            series => series.Name,
            series => series.DataKey,
            StringComparer.CurrentCulture
        );

        var columns = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [DateKey] = typeof(string),
        };

        foreach (var series in chartSeries)
        {
            columns[series.DataKey] = typeof(int);
        }

        var data = new DynamicChartData(columns);

        foreach (var dayGroup in uploads.GroupBy(upload => upload.Day).OrderBy(group => group.Key))
        {
            var row = data.AddRow();
            row.Set(DateKey, dayGroup.Key.ToString("d", CultureInfo.CurrentCulture));

            foreach (var upload in dayGroup)
            {
                row.Add(seriesKeyByHoster[upload.HosterName], upload.UploadCount);
            }
        }

        chartRows = data.Rows;

        chartConfig = ChartConfig.Create(
            chartSeries
                .Select(
                    (series, index) =>
                        (
                            series.DataKey,
                            new ChartSeriesConfig
                            {
                                Label = series.Name,
                                Color = $"var(--chart-{index % ChartColorCount + 1})",
                            }
                        )
                )
                .ToArray()
        );
    }

    private void UpdateReleaseStatusChart(ReleaseOnlineStateSummaryReadModel releaseSummary)
    {
        totalReleaseCount = releaseSummary.TotalReleaseCount;
        releaseStatusRows.Clear();

        foreach (var count in releaseSummary.Counts.Where(count => count.ReleaseCount > 0))
        {
            releaseStatusRows.Add(
                new ReleaseStatusChartRow(L.Localize(count.OnlineState), count.ReleaseCount)
            );
        }
    }

    private sealed record ChartSeries(string DataKey, string Name);

    private sealed record ReleaseStatusChartRow(string Name, int Value);
}
