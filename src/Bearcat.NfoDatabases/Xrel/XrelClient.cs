using System.Net;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel.Api;
using Refit;

namespace Bearcat.NfoDatabases.Xrel;

public class XrelClient(IXrelApi api, XrelRateLimitState rateLimitState)
{
    public async Task<XrelRelease?> GetReleaseInfoAsync(
        string dirname,
        CancellationToken cancellationToken = default
    )
    {
        rateLimitState.ThrowIfLimited();

        var response = await api.GetReleaseInfoAsync(dirname, cancellationToken);
        return HandleResponse(response);
    }

    public async Task<XrelP2PRelease?> GetP2pReleaseInfoAsync(
        string dirname,
        CancellationToken cancellationToken = default
    )
    {
        rateLimitState.ThrowIfLimited();

        var response = await api.GetP2pReleaseInfoAsync(dirname, cancellationToken);
        return HandleResponse(response);
    }

    public async Task<XrelExternalInfoDetails?> GetExternalInfoDetailsAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        rateLimitState.ThrowIfLimited();

        var response = await api.GetExternalInfoDetailsAsync(id, cancellationToken);
        return HandleResponse(response);
    }

    public async Task<IReadOnlyList<XrelExternalInfoMedia>> GetExternalInfoMediaAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        rateLimitState.ThrowIfLimited();

        var response = await api.GetExternalInfoMediaAsync(id, cancellationToken);
        return HandleResponse(response) ?? [];
    }

    private TResponse? HandleResponse<TResponse>(ApiResponse<TResponse> response)
    {
        rateLimitState.Update(response.Headers!);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new NfoDatabaseRateLimitExceededException("xREL", rateLimitState.GetResetAt());
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.Error is not null)
            {
                throw response.Error;
            }

            throw new HttpRequestException(
                $"xREL request failed with status code {response.StatusCode}."
            );
        }

        return response.Content;
    }
}
