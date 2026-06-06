using Bearcat.Domain.UseCases.ManageImageUploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ImageUploadBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ImageUploadBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Image upload";

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var imageUploadService = serviceProvider.GetRequiredService<ImageUploadService>();
        await imageUploadService.ProcessAsync(stoppingToken);
    }
}
