using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class BackgroundTaskState
{
    public int Id { get; set; }

    public required string Key { get; set; }

    public required string DisplayName { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastFinishedAt { get; set; }

    public BackgroundTaskExecutionStatus? LastExecutionStatus { get; set; }

    public string? LastErrorMessage { get; set; }

    public DateTime UpdatedAt { get; set; }
}
