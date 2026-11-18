using Bearcat.Abstractions.UploadCompletionAction.Dto;

namespace Bearcat.Abstractions.UploadCompletionAction;

public interface IUploadCompletionActionFactory
{
    IUploadCompletionAction GetByClassName(string className);

    IReadOnlyList<UploadCompletionActionDto> GetUploadCompletionActions();
}
