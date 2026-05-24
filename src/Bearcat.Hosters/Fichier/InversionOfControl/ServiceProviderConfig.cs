using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Fichier.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Fichier.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddFichier(this IServiceCollection services)
    {
        services
            .AddRefitClient<IFichierApi>(
                new RefitSettings
                {
                    ContentSerializer = new SystemTextJsonContentSerializer(
                        jsonSerializerOptions: new JsonSerializerOptions
                        {
                            NumberHandling = JsonNumberHandling.AllowReadingFromString,
                            PropertyNameCaseInsensitive = true,
                        }
                    ),
                }
            )
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(ApiClient.ApiBaseUrl);
                c.Timeout = Timeout.InfiniteTimeSpan;
            });

        services
            .AddHttpClient(
                name: ApiClient.UploadHttpClientName,
                configureClient: c =>
                {
                    c.Timeout = Timeout.InfiniteTimeSpan;
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            );

        services.AddScoped<IFichierApiClient, ApiClient>();
        services.AddKeyedScoped<IHoster, Fichier>(nameof(Fichier));
    }
}
