using Bearcat.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Bearcat.Api.UnitTest.Security;

public class ApiKeyAuthorizationFilterTest
{
    private const string ConfiguredApiKey = "configured-api-key";

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void OnAuthorization_NoApiKeyConfigured_Returns403(string? configuredApiKey)
    {
        // Arrange
        var filter = CreateFilter(configuredApiKey);
        var context = CreateContext(ConfiguredApiKey);

        // Act
        filter.OnAuthorization(context);

        // Assert
        AssertProblemResult(context, StatusCodes.Status403Forbidden);
    }

    [Test]
    public void OnAuthorization_MissingHeader_Returns401()
    {
        // Arrange
        var filter = CreateFilter(ConfiguredApiKey);
        var context = CreateContext(providedApiKey: null);

        // Act
        filter.OnAuthorization(context);

        // Assert
        AssertProblemResult(context, StatusCodes.Status401Unauthorized);
    }

    [Test]
    [TestCase("wrong-api-key")]
    [TestCase("configured-api-key-with-suffix")]
    [TestCase("CONFIGURED-API-KEY")]
    public void OnAuthorization_WrongApiKey_Returns401(string providedApiKey)
    {
        // Arrange
        var filter = CreateFilter(ConfiguredApiKey);
        var context = CreateContext(providedApiKey);

        // Act
        filter.OnAuthorization(context);

        // Assert
        AssertProblemResult(context, StatusCodes.Status401Unauthorized);
    }

    [Test]
    public void OnAuthorization_CorrectApiKey_DoesNotSetResult()
    {
        // Arrange
        var filter = CreateFilter(ConfiguredApiKey);
        var context = CreateContext(ConfiguredApiKey);

        // Act
        filter.OnAuthorization(context);

        // Assert
        context.Result.ShouldBeNull();
    }

    private static ApiKeyAuthorizationFilter CreateFilter(string? configuredApiKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [ApiKeyAuthorizationFilter.ApiKeyConfigurationKey] = configuredApiKey,
                }
            )
            .Build();

        return new ApiKeyAuthorizationFilter(configuration);
    }

    private static AuthorizationFilterContext CreateContext(string? providedApiKey)
    {
        var httpContext = new DefaultHttpContext();

        if (providedApiKey is not null)
        {
            httpContext.Request.Headers[ApiKeyHeader.Name] = providedApiKey;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }

    private static void AssertProblemResult(AuthorizationFilterContext context, int statusCode)
    {
        var result = context.Result.ShouldBeOfType<ObjectResult>();
        result.StatusCode.ShouldBe(statusCode);
        var problemDetails = result.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Status.ShouldBe(statusCode);
        problemDetails.Detail.ShouldNotBeNullOrWhiteSpace();
    }
}
