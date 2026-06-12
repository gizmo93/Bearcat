using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering;

public class ForumPostRenderService(
    IForumPostTemplateReadRepository templateReadRepository,
    IEnumerable<IForumPostRenderSource> renderSources
)
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

        return await RenderBodyAsync(template.TemplateBody, globals);
    }

    private IForumPostRenderSource? GetSource(ForumPostTemplateType type)
    {
        return renderSources.FirstOrDefault(source => source.Type == type);
    }

    private static async Task<ForumPostTemplateRenderResult> RenderBodyAsync(
        string templateBody,
        ScriptObject globals
    )
    {
        var template = Template.Parse(templateBody);

        if (template.HasErrors)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: template.Messages.Select(message => message.ToString()).ToList()
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

        context.PushGlobal(globals);

        try
        {
            var result = await template.RenderAsync(context);
            return new ForumPostTemplateRenderResult(Content: result, Errors: []);
        }
        catch (Exception exception)
            when (exception is ScriptRuntimeException or InvalidOperationException)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: [exception.Message]
            );
        }
    }
}
