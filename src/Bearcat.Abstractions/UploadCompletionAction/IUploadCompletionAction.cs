using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.UploadCompletionAction.Dto;

namespace Bearcat.Abstractions.UploadCompletionAction;

public interface IUploadCompletionAction
{
    string Name { get; }

    IReadOnlyList<ConfigurationField> ConfigurationFields { get; }

    IUploadCompletionActionConfig DeserializeConfig(string serializedConfig);

    Task<string> ExecuteAsync(
        IUploadCompletionActionConfig config,
        UploadCompletionActionInput input,
        CancellationToken cancellationToken
    );
}
