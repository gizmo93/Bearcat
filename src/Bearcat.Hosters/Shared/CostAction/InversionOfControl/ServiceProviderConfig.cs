using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared.CostAction.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Shared.CostAction.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddCostAction()
        {
            services
                .AddRefitClient<ICostActionApi>(
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
                    c.BaseAddress = new Uri(CostActionApiClient.ApiBaseUrl);
                });

            services.AddScoped<ICostActionApiClient, CostActionApiClient>();
        }
    }
}
