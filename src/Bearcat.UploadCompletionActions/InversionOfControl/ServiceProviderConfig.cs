using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.UploadCompletionActions.IntelligenXmlExport;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.UploadCompletionActions.InversionOfControl;

public static class ServiceProviderConfig
{
    extension(IServiceCollection services)
    {
        public void AddUploadCompletionActions()
        {
            services.AddKeyedScoped<IUploadCompletionAction, IntelligenXmlExportAction>(
                nameof(IntelligenXmlExportAction)
            );
            services.AddScoped<IUploadCompletionActionFactory, UploadCompletionActionFactory>();
        }
    }
}
