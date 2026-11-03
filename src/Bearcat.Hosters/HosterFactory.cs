using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Hosters;

public class HosterFactory(IServiceProvider serviceProvider) : IHosterFactory
{
    public IHoster GetByName(string name)
    {
        return serviceProvider.GetRequiredKeyedService<IHoster>(name);
    }

    public IReadOnlyList<HosterDto> GetHosterReadModels()
    {
        var hosters = serviceProvider.GetKeyedServices<IHoster>(KeyedService.AnyKey);

        return hosters
            .Select(h => new HosterDto(
                Name: h.Name,
                HosterClassName: h.GetType().Name,
                ConfigurationKeys: h.ConfigurationKeys,
                SupportsPremiumOnlyDownloads: h.SupportsPremiumOnlyDownloads,
                HasFixedParallelUploadLimit: h.HasFixedParallelUploadLimit,
                DefaultMaximumParallelUploads: h.DefaultMaximumParallelUploads,
                SupportsDownload: h is IHosterWithDownload,
                DownloadRequiresPremium: (h as IHosterWithDownload)?.DownloadRequiresPremium
                    ?? false
            ))
            .OrderBy(hoster => hoster.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyDictionary<string, IHoster> GetHostersByName()
    {
        var hosters = serviceProvider.GetKeyedServices<IHoster>(KeyedService.AnyKey);
        return hosters.ToDictionary(h => h.GetType().Name);
    }

    public IHoster? FindHosterByFileLink(string fileLink)
    {
        if (!Uri.TryCreate(fileLink.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var hosters = serviceProvider.GetKeyedServices<IHoster>(KeyedService.AnyKey);

        return hosters.FirstOrDefault(hoster =>
            FileLinkHostMatching.HostMatchesAnyHostName(uri.Host, hoster.FileLinkHostNames)
        );
    }
}
