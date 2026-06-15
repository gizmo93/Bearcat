using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Alfafile.InversionOfControl;
using Bearcat.Hosters.DDownload.InversionOfControl;
using Bearcat.Hosters.Fichier.InversionOfControl;
using Bearcat.Hosters.FileQ.InversionOfControl;
using Bearcat.Hosters.FileServe.InversionOfControl;
using Bearcat.Hosters.GoFile.InversionOfControl;
using Bearcat.Hosters.Katfile.InversionOfControl;
using Bearcat.Hosters.Keep2Share.InversionOfControl;
using Bearcat.Hosters.KrakenFiles.InversionOfControl;
using Bearcat.Hosters.Nitroflare.InversionOfControl;
using Bearcat.Hosters.Rapidgator.InversionOfControl;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.UploadG.InversionOfControl;
using Bearcat.Hosters.Uploady.InversionOfControl;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Hosters.InversionOfControl;

public static class ServiceProviderConfig
{
    public static void AddHosters(this IServiceCollection services)
    {
        services.AddAlfafile();
        services.AddRapidgator();
        services.AddDdownload();
        services.AddFileQ();
        services.AddFileServe();
        services.AddFichier();
        services.AddGoFile();
        services.AddKatfile();
        services.AddKeep2Share();
        services.AddKrakenFiles();
        services.AddNitroflare();
        services.AddUploadG();
        services.AddUploady();

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
