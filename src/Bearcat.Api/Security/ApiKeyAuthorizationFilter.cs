using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace Bearcat.Api.Security;

public class ApiKeyAuthorizationFilter(IConfiguration configuration) : IAuthorizationFilter
{
    public const string ApiKeyConfigurationKey = "Bearcat:ApiKey";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuredApiKey = configuration[ApiKeyConfigurationKey];

        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            context.Result = CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                $"Command endpoints are disabled because no API key is configured. Set {ApiKeyConfigurationKey} to enable them."
            );
            return;
        }

        var providedApiKey = context.HttpContext.Request.Headers[ApiKeyHeader.Name].ToString();

        if (!AreApiKeysEqual(configuredApiKey, providedApiKey))
        {
            context.Result = CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                $"The {ApiKeyHeader.Name} header is missing or does not match the configured API key."
            );
        }
    }

    private static bool AreApiKeysEqual(string configuredApiKey, string providedApiKey)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredApiKey),
            Encoding.UTF8.GetBytes(providedApiKey)
        );
    }

    private static ObjectResult CreateProblemResult(int statusCode, string title, string detail)
    {
        return new ObjectResult(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
            }
        )
        {
            StatusCode = statusCode,
        };
    }
}
