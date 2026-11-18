using Bearcat.Abstractions.UploadCompletionAction;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.UploadCompletionActions.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddUploadCompletionActions()
        {
            services.AddScoped<IUploadCompletionActionFactory, UploadCompletionActionFactory>();
        }
    }
}
