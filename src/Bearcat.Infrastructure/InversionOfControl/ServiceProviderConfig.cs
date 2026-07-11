using System.Net.Http.Headers;
using Bearcat.Abstractions;
using Bearcat.Abstractions.BackgroundTasks;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Security;
using Bearcat.Abstractions.Updates;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Infrastructure.BackgroundTasks;
using Bearcat.Infrastructure.Configuration;
using Bearcat.Infrastructure.Database.InversionOfControl;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.Infrastructure.Telegram;
using Bearcat.Infrastructure.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Infrastructure.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddInfrastructure(IConfiguration configuration)
        {
            services.AddSingleton<IEncryptionKeyProvider, FileEncryptionKeyProvider>();
            services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
            services.AddDatabase(configuration);
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddSingleton<
                IApplicationConfigurationOverrideCache,
                ApplicationConfigurationOverrideCache
            >();
            services.AddSingleton<IBackgroundTaskScheduleCache, BackgroundTaskScheduleCache>();

            services.AddHttpClient(
                GitHubUpdateChecker.HttpClientName,
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    // GitHub rejects requests without a User-Agent header.
                    client.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue("Bearcat", "1.0")
                    );
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
                    );
                }
            );
            services.AddSingleton<IAppVersionProvider, AssemblyAppVersionProvider>();
            services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
            services.AddHttpClient("telegram", client => client.Timeout = TimeSpan.FromSeconds(40));
            services.AddScoped<ITelegramClient, TelegramClient>();
        }
    }
}
