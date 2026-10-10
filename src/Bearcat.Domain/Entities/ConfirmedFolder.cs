namespace Bearcat.Domain.Entities;

public class ConfirmedFolder
{
    public int Id { get; set; }

    public required string Path { get; set; }

    public Guid MarkerId { get; set; }

    public DateTime ConfirmedAt { get; set; }
}
