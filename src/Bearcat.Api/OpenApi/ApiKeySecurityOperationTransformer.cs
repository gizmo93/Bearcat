using Bearcat.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bearcat.Api.OpenApi;

public class ApiKeySecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var requiresApiKey = context
            .Description.ActionDescriptor.EndpointMetadata.OfType<RequiresApiKeyAttribute>()
            .Any();

        if (!requiresApiKey)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(
            new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecuritySchemeReference(
                        ApiKeySecuritySchemeDocumentTransformer.SecuritySchemeId,
                        context.Document
                    )
                ] = [],
            }
        );

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(
            StatusCodes.Status401Unauthorized.ToString(),
            new OpenApiResponse
            {
                Description =
                    $"The {ApiKeyHeader.Name} header is missing or does not match the configured API key.",
            }
        );
        operation.Responses.TryAdd(
            StatusCodes.Status403Forbidden.ToString(),
            new OpenApiResponse
            {
                Description = "Command endpoints are disabled because no API key is configured.",
            }
        );

        return Task.CompletedTask;
    }
}
