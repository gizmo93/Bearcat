using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public class ForumPostTemplateFormModel
{
    public int? ForumPostTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ForumPostTemplateType Type { get; set; } = ForumPostTemplateType.Release;

    public string TemplateBody { get; set; } = string.Empty;
}
