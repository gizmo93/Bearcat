namespace Bearcat.Abstractions.UploadCompletionAction;

public interface IUploadCompletionActionConfig
{
    IReadOnlyDictionary<string, object?> ToDictionary();
}
