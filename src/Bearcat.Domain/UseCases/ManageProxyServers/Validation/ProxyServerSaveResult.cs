namespace Bearcat.Domain.UseCases.ManageProxyServers.Validation;

public record ProxyServerSaveResult(
    int? ProxyServerId,
    IReadOnlyList<ProxyServerValidationError> ValidationErrors
)
{
    public bool IsSuccess => ValidationErrors.Count == 0;

    public static ProxyServerSaveResult Saved(int proxyServerId)
    {
        return new ProxyServerSaveResult(proxyServerId, []);
    }

    public static ProxyServerSaveResult Invalid(
        IReadOnlyList<ProxyServerValidationError> validationErrors
    )
    {
        return new ProxyServerSaveResult(null, validationErrors);
    }
}
