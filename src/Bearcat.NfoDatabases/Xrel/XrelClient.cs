using System.Net;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel.Api;

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
        rateLimitState.Update(response.Headers);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
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
