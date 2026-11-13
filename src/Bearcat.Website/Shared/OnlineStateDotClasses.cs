using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Shared;

public static class OnlineStateDotClasses
{
    public static string GetDotClass(OnlineState onlineState)
    {
        return onlineState switch
        {
            OnlineState.Online => "h-2 w-2 shrink-0 rounded-full bg-muted-foreground/60",
            OnlineState.PartiallyOnline =>
                "h-2 w-2 shrink-0 rounded-full border-[1.5px] border-destructive",
            OnlineState.Offline => "h-2 w-2 shrink-0 rounded-full bg-destructive",
            OnlineState.Unknown =>
                "h-2.5 w-2.5 shrink-0 rounded-full border border-dashed border-muted-foreground",
            _ => throw new ArgumentOutOfRangeException(nameof(onlineState), onlineState, null),
        };
    }
}
