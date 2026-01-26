using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.LinkCrypters.HideCx.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Bearcat.LinkCrypters.HideCx.InversionOfControl;

public static class InversionOfControl
{
    extension(IServiceCollection services)
    {
        public void AddHideCx()
        {
            services.AddRefitClient<IHideCxApi>(
                    new RefitSettings
                    {
                        ContentSerializer = new SystemTextJsonContentSerializer(
                            jsonSerializerOptions: new JsonSerializerOptions
                            {
                                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                                PropertyNameCaseInsensitive = true,
                            })
                    })
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri("https://api.hide.cx");
                    c.Timeout = Timeout.InfiniteTimeSpan;
                });

            services.AddKeyedScoped<ILinkCrypter, HideCx>(nameof(HideCx));
        }
    }
}
