using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.DDownload.InversionOfControl;
using Bearcat.Hosters.GoFile.InversionOfControl;
using Bearcat.Hosters.Rapidgator.InversionOfControl;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Hosters.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddHosters(this IServiceCollection services)
    {
        services.AddRapidgator();
        services.AddDdownload();
        services.AddGoFile();

        services.AddScoped<IHosterFactory, HosterFactory>();
        services.AddHttpClient(
            name: HttpClientProvider.UploadHttpClientName,
            configureClient: c =>
            {
                c.Timeout = Timeout.InfiniteTimeSpan;
            }
        );

        services.AddScoped<HttpClientProvider>();
    }
}
