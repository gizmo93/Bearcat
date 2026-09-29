using Bearcat.Abstractions.Proxies;

namespace Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

public record ProxyCategoryDefaultReadModel(ProxyCategory ProxyCategory, int? ProxyServerId);
