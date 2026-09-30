using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.Transfers;

public sealed class TransferSpeedLimitService(
    IApplicationConfigurationProvider configurationProvider
) : IDisposable
{
    private const decimal BytesPerMegabyte = 1024 * 1024;

    private readonly Lock limiterUpdateLock = new();

    private TransferSpeedLimiter? globalUploadSpeedLimiter;

    private TransferSpeedLimiter? globalMirrorDownloadSpeedLimiter;

    public IDisposable EnterUploadScope(HosterRegistration registration)
    {
        var megabytesPerSecond = configurationProvider.GetValue<UploadConcurrencyConfiguration>(
            configuration => configuration.UploadSpeedLimitMegabytesPerSecond
        );

        TransferSpeedLimiter? globalLimiter;

        lock (limiterUpdateLock)
        {
            globalUploadSpeedLimiter = ApplyConfiguredLimit(
                currentLimiter: globalUploadSpeedLimiter,
                megabytesPerSecond: megabytesPerSecond
            );
            globalLimiter = globalUploadSpeedLimiter;
        }

        return EnterScope(globalLimiter);
    }

    public IDisposable EnterMirrorDownloadScope(HosterRegistration registration)
    {
        var megabytesPerSecond = configurationProvider.GetValue<DownloadConfiguration>(
            configuration => configuration.DownloadSpeedLimitMegabytesPerSecond
        );

        TransferSpeedLimiter? globalLimiter;

        lock (limiterUpdateLock)
        {
            globalMirrorDownloadSpeedLimiter = ApplyConfiguredLimit(
                currentLimiter: globalMirrorDownloadSpeedLimiter,
                megabytesPerSecond: megabytesPerSecond
            );
            globalLimiter = globalMirrorDownloadSpeedLimiter;
        }

        return EnterScope(globalLimiter);
    }

    public void Dispose()
    {
        lock (limiterUpdateLock)
        {
            globalUploadSpeedLimiter?.Dispose();
            globalUploadSpeedLimiter = null;
            globalMirrorDownloadSpeedLimiter?.Dispose();
            globalMirrorDownloadSpeedLimiter = null;
        }
    }

    private static IDisposable EnterScope(TransferSpeedLimiter? globalLimiter)
    {
        return TransferSpeedLimitScope.Enter(globalLimiter is null ? [] : [globalLimiter]);
    }

    private static TransferSpeedLimiter? ApplyConfiguredLimit(
        TransferSpeedLimiter? currentLimiter,
        decimal? megabytesPerSecond
    )
    {
        if (megabytesPerSecond is null or <= 0)
        {
            currentLimiter?.Dispose();
            return null;
        }

        var bytesPerSecond = ConvertMegabytesToBytes(megabytesPerSecond.Value);

        if (currentLimiter is null)
        {
            return new TransferSpeedLimiter(bytesPerSecond);
        }

        if (currentLimiter.BytesPerSecond != bytesPerSecond)
        {
            currentLimiter.ChangeBytesPerSecond(bytesPerSecond);
        }

        return currentLimiter;
    }

    private static int ConvertMegabytesToBytes(decimal megabytesPerSecond)
    {
        var bytesPerSecond = Math.Min(megabytesPerSecond, int.MaxValue) * BytesPerMegabyte;

        return (int)Math.Clamp(bytesPerSecond, 1, int.MaxValue);
    }
}
