using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.KrakenFiles.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.KrakenFiles.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddKrakenFiles()
        {
            services
                .AddRefitClient<IKrakenFilesApi>(
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
                    c.BaseAddress = new Uri("https://krakenfiles.com");
                    c.Timeout = Timeout.InfiniteTimeSpan;
                });

            services.AddScoped<IKrakenFilesApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, KrakenFiles>(nameof(KrakenFiles));
        }
    }
}
