using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.UploadCompletionActions;

public class UploadCompletionActionFactory(IServiceProvider serviceProvider)
    : IUploadCompletionActionFactory
{
    public IUploadCompletionAction GetByClassName(string className)
    {
        return serviceProvider.GetRequiredKeyedService<IUploadCompletionAction>(className);
    }

    public IReadOnlyList<UploadCompletionActionDto> GetUploadCompletionActions()
    {
        var uploadCompletionActions = serviceProvider.GetKeyedServices<IUploadCompletionAction>(
            KeyedService.AnyKey
        );

        return uploadCompletionActions
            .Select(uploadCompletionAction => new UploadCompletionActionDto(
                Name: uploadCompletionAction.Name,
                ClassName: uploadCompletionAction.GetType().Name,
                ConfigurationFields: uploadCompletionAction.ConfigurationFields
            ))
            .ToList();
    }
}
