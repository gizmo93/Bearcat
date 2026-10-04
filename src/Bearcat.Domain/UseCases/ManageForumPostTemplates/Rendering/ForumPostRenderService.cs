using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;

public class ForumPostRenderService(
    IForumPostTemplateReadRepository templateReadRepository,
    IEnumerable<IForumPostRenderSource> renderSources
) : IForumPostContentRenderer
{
    public IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables(
        ForumPostTemplateType type
    )
    {
        return GetSource(type)?.GetVariables() ?? [];
    }

    public async Task<ForumPostTemplateRenderResult> RenderAsync(
        int entityId,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var template = await templateReadRepository.GetDetailAsync(
            forumPostTemplateId: forumPostTemplateId,
            cancellationToken: cancellationToken
        );

        if (template is null)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: ["Forum post template not found."]
            );
        }

        var source = GetSource(template.Type);

        if (source is null)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: [$"No render source available for template type {template.Type}."]
            );
        }

        var globals =
            await source.BuildGlobalsAsync(entityId, cancellationToken) ?? new ScriptObject();

        var result = await RenderTemplateBodyAsync(template.TemplateBody, [globals]);

        return new ForumPostTemplateRenderResult(
            Content: result.Content,
            Errors: result.Errors.Select(FormatError).ToList()
        );
    }

    public async Task<ForumPostTemplatePreviewData?> LoadPreviewDataAsync(
        ForumPostTemplateType type,
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var source = renderSources.Single(renderSource => renderSource.Type == type);
        var globals = await source.BuildGlobalsAsync(entityId, cancellationToken);

        return globals is null
            ? null
            : new ForumPostTemplatePreviewData(
                globals,
                ForumPostTemplateDataNodeBuilder.Build(globals)
            );
    }

    public static Task<ForumPostTemplatePreviewResult> RenderPreviewAsync(
        ForumPostTemplatePreviewData previewData,
        string templateBody
    )
    {
        return RenderTemplateBodyAsync(
            templateBody,
            [previewData.Globals.Clone(deep: true), new ScriptObject()]
        );
    }

    private IForumPostRenderSource? GetSource(ForumPostTemplateType type)
    {
        return renderSources.FirstOrDefault(source => source.Type == type);
    }

    private static string FormatError(ForumPostTemplateError error)
    {
        return error.Line is null
            ? error.Message
            : $"Line {error.Line}, column {error.Column}: {error.Message}";
    }

    private static async Task<ForumPostTemplatePreviewResult> RenderTemplateBodyAsync(
        string templateBody,
        IReadOnlyList<IScriptObject> globalsInPushOrder
    )
    {
        var template = Template.Parse(templateBody);

        if (template.HasErrors)
        {
            return new ForumPostTemplatePreviewResult(
                Content: string.Empty,
                Errors: ForumPostTemplateErrorMapper.FromParserMessages(template.Messages)
            );
        }

        var context = new TemplateContext
        {
            StrictVariables = false,
            EnableRelaxedTargetAccess = true,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedIndexerAccess = true,
            MemberFilter = ForumPostTemplateVariableCatalog.ShouldExposeMember,
        };

        foreach (var globals in globalsInPushOrder)
        {
            context.PushGlobal(globals);
        }

        try
        {
            var result = await template.RenderAsync(context);
            return new ForumPostTemplatePreviewResult(Content: result, Errors: []);
        }
        catch (ScriptRuntimeException exception)
        {
            return new ForumPostTemplatePreviewResult(
                Content: string.Empty,
                Errors: [ForumPostTemplateErrorMapper.FromRuntimeException(exception)]
            );
        }
        catch (InvalidOperationException exception)
        {
            return new ForumPostTemplatePreviewResult(
                Content: string.Empty,
                Errors: [new ForumPostTemplateError(exception.Message, Line: null, Column: null)]
            );
        }
    }
}
