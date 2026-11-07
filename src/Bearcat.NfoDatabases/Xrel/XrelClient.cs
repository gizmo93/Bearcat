using System.Net;
using AngleSharp.Html.Parser;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel.Api;
using Refit;

namespace Bearcat.NfoDatabases.Xrel;

public class XrelClient(
    IXrelApi api,
    XrelRateLimitState rateLimitState,
    IHttpClientFactory httpClientFactory
)
{
    public const string NfoPageHttpClientName = "xrel-nfo-page";

    private readonly HtmlParser htmlParser = new();

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

    public async Task<string?> GetP2pNfoTextAsync(
        string nfoPageUrl,
        CancellationToken cancellationToken = default
    )
    {
        var httpClient = httpClientFactory.CreateClient(NfoPageHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, nfoPageUrl);
        request.Headers.UserAgent.ParseAdd("Bearcat/1.0");
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
        {
            throw new NfoDatabaseRateLimitExceededException("xREL", null);
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = await htmlParser.ParseDocumentAsync(html, cancellationToken);

        if (document.Title?.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new NfoDatabaseRateLimitExceededException("xREL", null);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"xREL NFO page request failed with status code {response.StatusCode}."
            );
        }

        return document.QuerySelector("#nfo_stripped pre")?.TextContent;
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
