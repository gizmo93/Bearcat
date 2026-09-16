using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Predb.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.NfoDatabases.Predb.InversionOfControl;

public static class ServiceProviderConfig
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public static void AddPredb(this IServiceCollection services)
    {
        services
            .AddRefitClient<IPredbApi>(
                new RefitSettings
                {
                    ContentSerializer = new SystemTextJsonContentSerializer(
                        new JsonSerializerOptions
                        {
                            NumberHandling = JsonNumberHandling.AllowReadingFromString,
                            PropertyNameCaseInsensitive = true,
                        }
                    ),
                }
            )
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.predb.net/");
                client.Timeout = RequestTimeout;
            });

        services.AddSingleton<PredbRateLimiter>();
        services.AddSingleton<PredbDownloadQuota>();
        services.AddHttpClient(
            PredbClient.DownloadHttpClientName,
            client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Bearcat/1.0");
                client.Timeout = RequestTimeout;
            }
        );
        services.AddScoped<PredbClient>();
        services.AddKeyedScoped<INfoDatabase, PredbNfoDatabase>(nameof(PredbNfoDatabase));
    }
}
