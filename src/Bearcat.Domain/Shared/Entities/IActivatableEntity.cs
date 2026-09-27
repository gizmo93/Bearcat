namespace Bearcat.Domain.Shared.Entities;

public interface IActivatableEntity
{
    bool IsActive { get; set; }
}
