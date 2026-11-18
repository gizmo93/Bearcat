using Bearcat.Abstractions.UploadCompletionAction;

namespace Bearcat.UploadCompletionActions.IntelligenXmlExport;

public sealed record IntelligenXmlExportConfig : IUploadCompletionActionConfig
{
    public required string ExportDirectory { get; init; }

    public string RelativeFilePathTemplate { get; init; } =
        IntelligenXmlExportAction.DefaultRelativeFilePathTemplate;

    public IReadOnlyDictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            { nameof(ExportDirectory), ExportDirectory },
            { nameof(RelativeFilePathTemplate), RelativeFilePathTemplate },
        };
    }
}
