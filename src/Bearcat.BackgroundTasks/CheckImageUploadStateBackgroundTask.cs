using Bearcat.Domain.UseCases.ManageImageUploads.OnlineChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public class CheckImageUploadStateBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CheckImageUploadStateBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Image upload state check";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var imageUploadOnlineCheckService =
            serviceProvider.GetRequiredService<ImageUploadOnlineCheckService>();
        await imageUploadOnlineCheckService.CheckOnlineStateOfLeastRecentlyCheckedImageUploadsAsync(
            stoppingToken
        );
    }
}
