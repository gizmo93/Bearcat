using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bearcat.Infrastructure.Database;

public static class ChangeTrackerExtensions
{
    extension(ChangeTracker changeTracker)
    {
        public void DiscardPendingChanges()
        {
            foreach (var entry in changeTracker.Entries().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                    case EntityState.Modified:
                    case EntityState.Deleted:
                        entry.CurrentValues.SetValues(entry.OriginalValues);
                        entry.State = EntityState.Unchanged;
                        break;
                }
            }
        }
    }
}
