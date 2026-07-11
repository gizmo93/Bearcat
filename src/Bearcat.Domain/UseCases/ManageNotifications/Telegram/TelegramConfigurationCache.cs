namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed class TelegramConfigurationCache
{
    private readonly Lock sync = new();
    private TelegramConfigurationState? state;
    private bool initialized;

    public bool IsInitialized
    {
        get
        {
            lock (sync)
            {
                return initialized;
            }
        }
    }

    public TelegramConfigurationState? Current
    {
        get
        {
            lock (sync)
            {
                return state;
            }
        }
    }

    public void Set(TelegramConfigurationState? loadedState)
    {
        lock (sync)
        {
            state = loadedState;
            initialized = true;
        }
    }

    public void Invalidate()
    {
        lock (sync)
        {
            state = null;
            initialized = false;
        }
    }
}
