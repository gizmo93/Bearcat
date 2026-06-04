using System.Globalization;
using Bearcat.Domain.UseCases.Dashboard.ReadModels;
using Bearcat.Domain.UseCases.Dashboard.Repositories;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.Dashboard;

public partial class DashboardPage(IDashboardReadRepository readRepository)
{
    private const int MaxHosterSeries = 20;
    private const int ChartColorCount = 20;
    private const string DateKey = nameof(ChartRow.Date);

    private readonly List<ChartRow> chartRows = [];
    private readonly List<ChartSeries> chartSeries = [];
    private ChartConfig chartConfig = ChartConfig.Create();
    private DateRange? dateRange = GetCurrentWeekDateRange();
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
            UpdateChart(uploads);
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
        chartRows.Clear();
        chartSeries.Clear();

        var allHosterNames = uploads
            .Select(upload => upload.HosterName)
            .Distinct(StringComparer.CurrentCulture)
            .Order(StringComparer.CurrentCulture)
            .ToList();

        var hosterNames = allHosterNames.Take(MaxHosterSeries).ToList();
        var hasOtherHosters = allHosterNames.Count > MaxHosterSeries;

        for (var index = 0; index < hosterNames.Count; index++)
        {
            chartSeries.Add(new ChartSeries($"Hoster{index}", hosterNames[index]));
        }

        if (hasOtherHosters)
        {
            chartSeries.Add(new ChartSeries($"Hoster{MaxHosterSeries}", L["OtherHosters"]));
        }

        var seriesKeyByHoster = chartSeries.ToDictionary(
            series => series.Name,
            series => series.DataKey,
            StringComparer.CurrentCulture
        );

        foreach (var dayGroup in uploads.GroupBy(upload => upload.Day).OrderBy(group => group.Key))
        {
            var row = new ChartRow
            {
                Date = dayGroup.Key.ToString("d", CultureInfo.CurrentCulture),
            };

            foreach (var upload in dayGroup)
            {
                var dataKey = seriesKeyByHoster.GetValueOrDefault(
                    upload.HosterName,
                    $"Hoster{MaxHosterSeries}"
                );

                row.AddValue(dataKey, upload.UploadCount);
            }

            chartRows.Add(row);
        }

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

    private sealed record ChartSeries(string DataKey, string Name);

    private sealed class ChartRow
    {
        public string Date { get; init; } = string.Empty;

        public int Hoster0 { get; set; }
        public int Hoster1 { get; set; }
        public int Hoster2 { get; set; }
        public int Hoster3 { get; set; }
        public int Hoster4 { get; set; }
        public int Hoster5 { get; set; }
        public int Hoster6 { get; set; }
        public int Hoster7 { get; set; }
        public int Hoster8 { get; set; }
        public int Hoster9 { get; set; }
        public int Hoster10 { get; set; }
        public int Hoster11 { get; set; }
        public int Hoster12 { get; set; }
        public int Hoster13 { get; set; }
        public int Hoster14 { get; set; }
        public int Hoster15 { get; set; }
        public int Hoster16 { get; set; }
        public int Hoster17 { get; set; }
        public int Hoster18 { get; set; }
        public int Hoster19 { get; set; }
        public int Hoster20 { get; set; }

        public void AddValue(string dataKey, int value)
        {
            switch (dataKey)
            {
                case nameof(Hoster0):
                    Hoster0 += value;
                    break;
                case nameof(Hoster1):
                    Hoster1 += value;
                    break;
                case nameof(Hoster2):
                    Hoster2 += value;
                    break;
                case nameof(Hoster3):
                    Hoster3 += value;
                    break;
                case nameof(Hoster4):
                    Hoster4 += value;
                    break;
                case nameof(Hoster5):
                    Hoster5 += value;
                    break;
                case nameof(Hoster6):
                    Hoster6 += value;
                    break;
                case nameof(Hoster7):
                    Hoster7 += value;
                    break;
                case nameof(Hoster8):
                    Hoster8 += value;
                    break;
                case nameof(Hoster9):
                    Hoster9 += value;
                    break;
                case nameof(Hoster10):
                    Hoster10 += value;
                    break;
                case nameof(Hoster11):
                    Hoster11 += value;
                    break;
                case nameof(Hoster12):
                    Hoster12 += value;
                    break;
                case nameof(Hoster13):
                    Hoster13 += value;
                    break;
                case nameof(Hoster14):
                    Hoster14 += value;
                    break;
                case nameof(Hoster15):
                    Hoster15 += value;
                    break;
                case nameof(Hoster16):
                    Hoster16 += value;
                    break;
                case nameof(Hoster17):
                    Hoster17 += value;
                    break;
                case nameof(Hoster18):
                    Hoster18 += value;
                    break;
                case nameof(Hoster19):
                    Hoster19 += value;
                    break;
                case nameof(Hoster20):
                    Hoster20 += value;
                    break;
            }
        }
    }
}
