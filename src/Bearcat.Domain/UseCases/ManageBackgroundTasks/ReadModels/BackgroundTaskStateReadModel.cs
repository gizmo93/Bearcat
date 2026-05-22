using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageBackgroundTasks.ReadModels;

public record BackgroundTaskStateReadModel(
    int Id,
    string Key,
    string DisplayName,
    bool IsEnabled,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    BackgroundTaskExecutionStatus? LastExecutionStatus,
    string? LastErrorMessage,
    DateTime UpdatedAt
);
