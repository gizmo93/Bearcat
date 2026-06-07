using System.Globalization;
using Bearcat.Domain.UseCases.ManageBackgroundTasks;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.ReadModels;
using Bearcat.Domain.UseCases.ManageBackgroundTasks.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageBackgroundTasks;

public partial class BackgroundTasksPage(
    IBackgroundTaskStateReadRepository readRepository,
    ToastService toastService
)
{
    private BackgroundTaskStateService backgroundTaskStateService = null!;
    private IReadOnlyList<BackgroundTaskStateReadModel> backgroundTasks = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        backgroundTaskStateService =
            ScopedServices.GetRequiredService<BackgroundTaskStateService>();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            backgroundTasks = await readRepository.GetAllAsync(CancellationToken.None);
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
        await backgroundTaskStateService.SetIsEnabledAsync(
            task.Id,
            isEnabled,
            CancellationToken.None
        );

        toastService.Success(
            isEnabled
                ? L["BackgroundTaskEnabled", task.DisplayName]
                : L["BackgroundTaskDisabled", task.DisplayName]
        );
        await LoadAsync();
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
        return value?.ToString("g", CultureInfo.CurrentCulture) ?? "-";
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
            _ => $"{duration.TotalMinutes:0.#}m"
        };
    }

    private static bool IsRunning(BackgroundTaskStateReadModel task)
    {
        return task.LastStartedAt.HasValue
            && (!task.LastFinishedAt.HasValue || task.LastFinishedAt < task.LastStartedAt);
    }
}
