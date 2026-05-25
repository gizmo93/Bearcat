namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ForumPostTemplateVariableAttribute(string description) : Attribute
{
    public string Description { get; } = description;

    public bool IncludeChildren { get; set; }

    public string? LoopVariable { get; set; }

    public Type? ElementType { get; set; }
}
