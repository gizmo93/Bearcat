namespace Bearcat.Domain.Entities;

public class RemoteSourceRegistration
{
    public const int DefaultMaxConnections = 2;

    public int Id { get; set; }

    public required string Name { get; set; }

    public required string SerializedConfig { get; set; }

    public required string SourceClassName { get; set; }

    public bool IsActive { get; set; }

    public int MaxConnections { get; set; } = DefaultMaxConnections;
}
