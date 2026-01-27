using Bearcat.Domain.Abstractions;

namespace Bearcat.Domain.Entities;

public class LinkCrypterContainerNotification : EntityNotification<LinkCrypterContainer>
{
    public LinkCrypterContainerNotification(LinkCrypterContainer entity)
    {
        Entity = entity;
    }

    // EF Core
    public LinkCrypterContainerNotification()
    {

    }
}
