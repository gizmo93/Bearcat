using System.Text.Json;
using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Scriban;
using Scriban.Runtime;

namespace Bearcat.UploadCompletionActions.IntelligenXmlExport;

public sealed class IntelligenXmlExportAction : IUploadCompletionAction
{
    public const string DefaultRelativeFilePathTemplate = "{{ release.name }}.xml";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly IReadOnlyList<ConfigurationField> Fields =
    [
        new(
            nameof(IntelligenXmlExportConfig.ExportDirectory),
            ConfigurationFieldType.Folder,
            IsRequired: true
        ),
        new(
            nameof(IntelligenXmlExportConfig.RelativeFilePathTemplate),
            ConfigurationFieldType.Text,
            IsRequired: true,
            DefaultValue: DefaultRelativeFilePathTemplate
        ),
    ];

    public string Name => "IntelliGeN XML export";

    public IReadOnlyList<ConfigurationField> ConfigurationFields => Fields;

    public IUploadCompletionActionConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<IntelligenXmlExportConfig>(
                serializedConfig,
                SerializerOptions
            ) ?? throw new JsonException("IntelliGeN XML export configuration is empty");
    }

    public async Task<string> ExecuteAsync(
        IUploadCompletionActionConfig config,
        UploadCompletionActionInput input,
        CancellationToken cancellationToken
    )
    {
        var exportConfig = (IntelligenXmlExportConfig)config;
        var relativeFilePath = RenderRelativeFilePath(exportConfig.RelativeFilePathTemplate, input);

        var filePath = IntelligenXmlExportFilePathBuilder.BuildUnusedFilePath(
            exportConfig.ExportDirectory,
            relativeFilePath
        );

        var document = IntelligenXmlDocumentBuilder.Build(input);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await File.WriteAllBytesAsync(
            filePath,
            IntelligenXmlDocumentBuilder.ToUtf8Bytes(document),
            cancellationToken
        );

        return filePath;
    }

    private static string RenderRelativeFilePath(
        string relativeFilePathTemplate,
        UploadCompletionActionInput input
    )
    {
        var template = Template.Parse(relativeFilePathTemplate);

        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Relative file path template is invalid: {string.Join(", ", template.Messages)}"
            );
        }

        var globals = new ScriptObject
        {
            ["release"] = input.Release,
            ["is_reupload"] = input.IsReupload,
        };

        var context = new TemplateContext();
        context.PushGlobal(globals);

        return template.Render(context);
    }
}
