using Bearcat.Abstractions.ConfigurationFields;

namespace Bearcat.Abstractions.UploadCompletionAction.Dto;

public sealed record UploadCompletionActionDto(
    string Name,
    string ClassName,
    IReadOnlyList<ConfigurationField> ConfigurationFields
);
