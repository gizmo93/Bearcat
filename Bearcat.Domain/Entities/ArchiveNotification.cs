using Bearcat.Domain.Abstractions;

namespace Bearcat.Domain.Entities;

public class ArchiveNotification : EntityNotification<Archive>
{
    public ArchiveNotification(Archive archive)
    {
        Entity = archive;
    }

    // EF Core
    public ArchiveNotification()
    {

    }
}
