using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.LinkCrypters.FileCrypt.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.LinkCrypters.FileCrypt.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddFileCrypt()
        {
            services
                .AddRefitClient<IFileCryptApi>(
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
                    c.BaseAddress = new Uri("https://www.filecrypt.cc");
                });

            services.AddKeyedScoped<ILinkCrypter, FileCrypt>(nameof(FileCrypt));
        }
    }
}
