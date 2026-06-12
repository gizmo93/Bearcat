using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates;

public class ForumPostTemplateService(IForumPostTemplateWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        string name,
        ForumPostTemplateType type,
        string? templateBody,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTime.UtcNow;
        var template = new ForumPostTemplate
        {
            Name = name.Trim(),
            Type = type,
            TemplateBody = templateBody ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };

        writeRepository.Add(template);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return template.Id;
    }

    public async Task UpdateAsync(
        int forumPostTemplateId,
        string name,
        ForumPostTemplateType type,
        string? templateBody,
        CancellationToken cancellationToken = default
    )
    {
        var template = await writeRepository.GetByIdAsync(forumPostTemplateId, cancellationToken);
        template.Name = name.Trim();
        template.Type = type;
        template.TemplateBody = templateBody ?? string.Empty;
        template.UpdatedAt = DateTime.UtcNow;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var template = await writeRepository.GetByIdAsync(forumPostTemplateId, cancellationToken);
        writeRepository.Remove(template);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public static ForumPostTemplateValidationResult Validate(string? templateBody)
    {
        var template = Template.Parse(templateBody ?? string.Empty);
        return new ForumPostTemplateValidationResult(
            IsValid: !template.HasErrors,
            Errors: template.Messages.Select(message => message.ToString()).ToList()
        );
    }
}
