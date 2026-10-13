namespace Bearcat.Domain.UseCases.ManageUploads.HosterAccountPoolSelection;

public class HosterStorageAvailabilityCache
{
    private readonly Lock availabilityLock = new();
    private readonly Dictionary<
        int,
        HosterStorageAvailability
    > availabilityByHosterRegistrationId = [];

    public HosterStorageAvailability? GetUnexpired(int hosterRegistrationId, DateTime now)
    {
        lock (availabilityLock)
        {
            return
                availabilityByHosterRegistrationId.TryGetValue(
                    hosterRegistrationId,
                    out var availability
                )
                && availability.ExpiresAt > now
                ? availability
                : null;
        }
    }

    public void Set(int hosterRegistrationId, HosterStorageAvailability availability)
    {
        lock (availabilityLock)
        {
            availabilityByHosterRegistrationId[hosterRegistrationId] = availability;
        }
    }

    public void ReduceAvailableBytes(int hosterRegistrationId, long bytes)
    {
        lock (availabilityLock)
        {
            var availability = availabilityByHosterRegistrationId[hosterRegistrationId];

            availabilityByHosterRegistrationId[hosterRegistrationId] = availability with
            {
                AvailableBytes = availability.AvailableBytes - bytes,
            };
        }
    }
}
