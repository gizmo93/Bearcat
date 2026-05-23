using System.Net.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Keep2Share.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.Hosters.Keep2Share.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddKeep2Share()
        {
            services
                .AddRefitClient<IKeep2ShareApi>(
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
                    c.BaseAddress = new Uri("https://keep2share.cc/api/v2");
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
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, sslPolicyErrors) =>
                        {
                            // The hoster sometimes the first try returns URLs that don't match its SSL certificate
                            const SslPolicyErrors toleratedErrors =
                                SslPolicyErrors.RemoteCertificateNameMismatch
                                | SslPolicyErrors.RemoteCertificateChainErrors;

                            return sslPolicyErrors == SslPolicyErrors.None
                                || (sslPolicyErrors & ~toleratedErrors) == SslPolicyErrors.None;
                        },
                    }
                );

            services.AddScoped<IKeep2ShareApiClient, ApiClient>();
            services.AddKeyedScoped<IHoster, Keep2Share>(nameof(Keep2Share));
        }
    }
}
