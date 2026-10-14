using Bearcat.Domain.UseCases.ManageUploads.OnlineChecks;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.BackgroundTasks;

public class CheckUploadStateBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CheckUploadStateBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Upload state check";

    protected override TimeSpan DefaultInterval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var uploadOnlineCheckService =
            serviceProvider.GetRequiredService<UploadOnlineCheckService>();
        var uploadCreationService = serviceProvider.GetRequiredService<UploadCreationService>();
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        var localNow = timeProvider.GetLocalNow();

        await uploadOnlineCheckService.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            localNow,
            stoppingToken
        );
        await uploadCreationService.CreateMissingInitialUploadsAsync(localNow, stoppingToken);
        await uploadCreationService.CreateAutomaticReuploadsForOfflineUploadsAsync(
            localNow,
            stoppingToken
        );
    }
}
