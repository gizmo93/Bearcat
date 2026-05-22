using Bearcat.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Application.BackgroundTasks;

public class CheckUploadStateBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CheckUploadStateBackgroundTask> logger
) : BearcatBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Upload state check";

    protected override TimeSpan Interval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var uploadStateService = serviceProvider.GetRequiredService<UploadStateService>();
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();

        await uploadStateService.CheckUploadStatesAsync(timeProvider.GetLocalNow(), stoppingToken);
    }
}
