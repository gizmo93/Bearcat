using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Proxies;
using Bearcat.Hosters.FileFactory.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.FileFactory.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddFileFactory()
        {
            services.AddFileFactoryRefitClient<IFileFactoryWebApi>(ApiClient.WebApiBaseUrl);
            services.AddFileFactoryRefitClient<IFileFactoryConvexApi>(ApiClient.ConvexApiBaseUrl);

            services.AddScoped<IFileFactoryApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, FileFactory>(nameof(FileFactory));
        }

        private void AddFileFactoryRefitClient<TApi>(string baseUrl)
            where TApi : class
        {
            services
                .AddRefitClient<TApi>(
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
                    c.BaseAddress = new Uri(baseUrl);
                    c.Timeout = TimeSpan.FromMinutes(2);
                })
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false }
                )
                .UseProxyForCategory(ProxyCategory.HosterUploads);
        }
    }
}
