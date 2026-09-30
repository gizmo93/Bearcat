using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Exceptions;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public static class XFilesharingApiKeyRejection
{
    private const string InvalidKeyMessage = "Invalid key";

    public static void ThrowIfRejected(int status, string? message)
    {
        if (
            status == (int)HttpStatusCode.BadRequest
            && string.Equals(message?.Trim(), InvalidKeyMessage, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new HosterCredentialsRejectedException(message!.Trim());
        }
    }

    public static void ThrowIfRejected(HttpStatusCode? httpStatusCode, string? responseContent)
    {
        if (
            httpStatusCode != HttpStatusCode.Unauthorized
            || string.IsNullOrWhiteSpace(responseContent)
        )
        {
            return;
        }

        StatusResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<StatusResponse>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException)
        {
            return;
        }

        if (response?.Status == (int)HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException(
                string.IsNullOrWhiteSpace(response.Msg) ? "Unauthorized" : response.Msg
            );
        }
    }
}
