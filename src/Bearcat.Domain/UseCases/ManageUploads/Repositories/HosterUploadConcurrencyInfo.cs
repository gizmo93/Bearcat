using Bearcat.Abstractions.Proxies;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public record HosterUploadConcurrencyInfo(
    string SerializedConfig,
    int? MaxParallelUploadsOverride,
    ProxySelection UploadProxySelection,
    int? UploadProxyServerId
);
