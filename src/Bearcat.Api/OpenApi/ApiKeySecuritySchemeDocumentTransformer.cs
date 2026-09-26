using Bearcat.Api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bearcat.Api.OpenApi;

public class ApiKeySecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string SecuritySchemeId = "ApiKey";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SecuritySchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyHeader.Name,
            Description = "API key required by command endpoints.",
        };

        return Task.CompletedTask;
    }
}
