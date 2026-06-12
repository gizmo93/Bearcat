using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ForumPostTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ForumPostTemplateType Type { get; set; } = ForumPostTemplateType.Release;

    public string TemplateBody { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
