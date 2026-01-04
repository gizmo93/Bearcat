using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class Release
{
    public int Id { get; set; }
    
    public string Name { get; set; } = null!;

    public ReleaseType ReleaseType { get; set; }
    
    public List<Distribution> Distributions { get; set; } = new();
}
