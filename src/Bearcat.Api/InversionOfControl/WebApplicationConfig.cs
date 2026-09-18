using Microsoft.AspNetCore.Builder;
using Scalar.AspNetCore;

namespace Bearcat.Api.InversionOfControl;

public static class WebApplicationConfig
{
    extension(WebApplication app)
    {
        public void MapApi()
        {
            app.MapOpenApi();
            app.MapScalarApiReference(
                "/api/docs",
                options =>
                    options.AddDocument(
                        "v1",
                        title: "Bearcat API",
                        routePattern: "/openapi/{documentName}.json"
                    )
            );
        }
    }
}
