using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.FileServe.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.FileServe.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddFileServe()
        {
            services
                .AddRefitClient<IFileServeApi>(
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

            services.AddScoped<IFileServeApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, FileServe>(nameof(FileServe));
        }
    }
}
