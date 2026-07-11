using System.Globalization;
using Bearcat.Domain.UseCases.ManageBackgroundTasks;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.ReadModels;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageBackgroundTasks;

public partial class BackgroundTasksPage(
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<BackgroundTaskStateReadModel> backgroundTasks = [];
    private bool isLoading;

    private BackgroundTaskStateReadModel? editingTask;
    private bool isIntervalDialogOpen;
    private string intervalValue = string.Empty;
    private IntervalUnit intervalUnit = IntervalUnit.Seconds;
    private string? intervalError;

    private List<SelectOption<IntervalUnit>> IntervalUnitOptions =>
        [
            new(IntervalUnit.Seconds, L["UnitSeconds"]),
            new(IntervalUnit.Minutes, L["UnitMinutes"]),
            new(IntervalUnit.Hours, L["UnitHours"]),
        ];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            backgroundTasks = await operationRunner.RunAsync(
                (IBackgroundTaskStateReadRepository repository) =>
                    repository.GetAllAsync(CancellationToken.None)
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task SetIsEnabledAsync(BackgroundTaskStateReadModel task, bool isEnabled)
    {
        await operationRunner.RunAsync(
            (BackgroundTaskStateService service) =>
                service.SetIsEnabledAsync(task.Id, isEnabled, CancellationToken.None)
        );

        toastService.Success(
            isEnabled
                ? L["BackgroundTaskEnabled", task.DisplayName]
                : L["BackgroundTaskDisabled", task.DisplayName]
        );
        await LoadAsync();
    }

    private static TimeSpan GetEffectiveInterval(BackgroundTaskStateReadModel task)
    {
        return task.IntervalOverride ?? task.DefaultInterval;
    }

    private void OpenIntervalDialog(BackgroundTaskStateReadModel task)
    {
        editingTask = task;
        var (value, unit) = DecomposeInterval(task.IntervalOverride ?? task.DefaultInterval);
        intervalValue = value.ToString(CultureInfo.InvariantCulture);
        intervalUnit = unit;
        intervalError = null;
        isIntervalDialogOpen = true;
    }

    private async Task SaveIntervalAsync()
    {
        if (editingTask is null)
        {
            return;
        }

        if (
            !long.TryParse(
                intervalValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value
            )
            || value <= 0
        )
        {
            intervalError = L["IntervalInvalid"];
            return;
        }

        var interval = ToInterval(value, intervalUnit);

        if (interval < BackgroundTaskStateService.MinInterval)
        {
            intervalError = L[
                "IntervalTooSmall",
                BackgroundTaskStateService.MinInterval.TotalSeconds.ToString(
                    "0",
                    CultureInfo.CurrentCulture
                )
            ];
            return;
        }

        await operationRunner.RunAsync(
            (BackgroundTaskStateService service) =>
                service.SetIntervalOverrideAsync(editingTask.Id, interval, CancellationToken.None)
        );

        toastService.Success(L["IntervalUpdated", editingTask.DisplayName]);
        isIntervalDialogOpen = false;
        await LoadAsync();
    }

    private async Task ResetIntervalAsync()
    {
        if (editingTask is null)
        {
            return;
        }

        await operationRunner.RunAsync(
            (BackgroundTaskStateService service) =>
                service.SetIntervalOverrideAsync(editingTask.Id, null, CancellationToken.None)
        );

        toastService.Success(L["IntervalResetToDefault", editingTask.DisplayName]);
        isIntervalDialogOpen = false;
        await LoadAsync();
    }

    private static (long Value, IntervalUnit Unit) DecomposeInterval(TimeSpan interval)
    {
        var totalSeconds = (long)interval.TotalSeconds;

        if (totalSeconds >= 3600 && totalSeconds % 3600 == 0)
        {
            return (totalSeconds / 3600, IntervalUnit.Hours);
        }

        if (totalSeconds >= 60 && totalSeconds % 60 == 0)
        {
            return (totalSeconds / 60, IntervalUnit.Minutes);
        }

        return (totalSeconds, IntervalUnit.Seconds);
    }

    private static TimeSpan ToInterval(long value, IntervalUnit unit)
    {
        return unit switch
        {
            IntervalUnit.Hours => TimeSpan.FromHours(value),
            IntervalUnit.Minutes => TimeSpan.FromMinutes(value),
            _ => TimeSpan.FromSeconds(value),
        };
    }

    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalSeconds < 60)
        {
            return $"{interval.TotalSeconds:0} s";
        }

        if (interval.TotalMinutes < 60)
        {
            return interval.Seconds == 0
                ? $"{interval.TotalMinutes:0} min"
                : $"{interval.Minutes} min {interval.Seconds} s";
        }

        return interval.Minutes == 0
            ? $"{interval.TotalHours:0} h"
            : $"{(int)interval.TotalHours} h {interval.Minutes} min";
    }

    private string GetStatusLabel(BackgroundTaskStateReadModel task)
    {
        if (IsRunning(task))
        {
            return L["Running"];
        }

        return task.LastExecutionStatus switch
        {
            BackgroundTaskExecutionStatus.Success => L["Success"],
            BackgroundTaskExecutionStatus.Error => L["Error"],
            _ => L["NeverRun"],
        };
    }

    private static BadgeVariant GetStatusVariant(BackgroundTaskStateReadModel task)
    {
        if (IsRunning(task))
        {
            return BadgeVariant.Secondary;
        }

        return task.LastExecutionStatus switch
        {
            BackgroundTaskExecutionStatus.Success => BadgeVariant.Default,
            BackgroundTaskExecutionStatus.Error => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("G", CultureInfo.CurrentCulture) ?? "-";
    }

    private static string FormatDuration(BackgroundTaskStateReadModel task)
    {
        if (!task.LastStartedAt.HasValue || !task.LastFinishedAt.HasValue)
        {
            return "-";
        }

        var duration = task.LastFinishedAt.Value - task.LastStartedAt.Value;

        return duration.TotalSeconds switch
        {
            < 1 => "< 1s",
            < 60 => $"{duration.TotalSeconds:0}s",
            _ => $"{duration.TotalMinutes:0.#}m",
        };
    }

    private static bool IsRunning(BackgroundTaskStateReadModel task)
    {
        return task.LastStartedAt.HasValue
            && (!task.LastFinishedAt.HasValue || task.LastFinishedAt < task.LastStartedAt);
    }

    private enum IntervalUnit
    {
        Seconds,
        Minutes,
        Hours,
    }
}
