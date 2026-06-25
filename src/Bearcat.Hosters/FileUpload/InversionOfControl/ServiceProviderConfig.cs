using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.FileUpload.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.FileUpload.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddFileUpload()
        {
            services
                .AddRefitClient<IFileUploadApi>(
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

            services.AddScoped<IFileUploadApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, FileUpload>(nameof(FileUpload));
        }
    }
}
