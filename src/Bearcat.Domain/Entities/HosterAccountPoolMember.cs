namespace Bearcat.Domain.Entities;

public class HosterAccountPoolMember
{
    public int Id { get; set; }

    public int HosterAccountPoolId { get; set; }

    public HosterAccountPool HosterAccountPool { get; set; } = null!;

    public int HosterRegistrationId { get; set; }

    public HosterRegistration HosterRegistration { get; set; } = null!;

    public int Priority { get; set; }
}
