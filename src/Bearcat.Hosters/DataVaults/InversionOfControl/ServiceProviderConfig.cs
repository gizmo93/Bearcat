using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.DataVaults.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.DataVaults.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddDataVaults()
        {
            services
                .AddRefitClient<IDataVaultsApi>(
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

            services.AddScoped<IDataVaultsApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, DataVaults>(nameof(DataVaults));
        }
    }
}
