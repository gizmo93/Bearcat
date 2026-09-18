using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Api.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddApi()
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(ServiceProviderConfig).Assembly)
                .AddControllersAsServices()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
                );
            services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
            );
            services.AddProblemDetails();
            services.AddOpenApi(
                "v1",
                options =>
                {
                    options.ShouldInclude = description =>
                        description.RelativePath?.StartsWith("api/", StringComparison.Ordinal)
                            is true;
                    options.AddDocumentTransformer(
                        (document, context, cancellationToken) =>
                        {
                            document.Info.Title = "Bearcat API";
                            return Task.CompletedTask;
                        }
                    );
                }
            );
        }
    }
}
