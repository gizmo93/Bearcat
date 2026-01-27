using Bearcat.Domain.Abstractions;

namespace Bearcat.Domain.Entities;

public class UploadNotification : EntityNotification<Upload>
{
    public UploadNotification(Upload upload)
    {
        Entity = upload;
    }

    // EF Core
    public UploadNotification()
    {

    }
}
