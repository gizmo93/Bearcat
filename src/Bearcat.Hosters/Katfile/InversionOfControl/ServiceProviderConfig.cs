using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Katfile.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Katfile.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddKatfile()
        {
            services
                .AddRefitClient<IKatfileApi>(
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

            services.AddScoped<IKatfileApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, Katfile>(nameof(Katfile));
        }
    }
}
