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

    private readonly SpeedLimitersForTransferDirection uploadSpeedLimiters = new();

    private readonly SpeedLimitersForTransferDirection mirrorDownloadSpeedLimiters = new();

    public IDisposable EnterUploadScope(HosterRegistration registration)
    {
        var globalMegabytesPerSecond =
            configurationProvider.GetValue<UploadConcurrencyConfiguration>(configuration =>
                configuration.UploadSpeedLimitMegabytesPerSecond
            );

        return EnterScope(
            speedLimiters: uploadSpeedLimiters,
            hosterRegistrationId: registration.Id,
            hosterRegistrationMegabytesPerSecond: registration.UploadSpeedLimitMegabytesPerSecond,
            globalMegabytesPerSecond: globalMegabytesPerSecond
        );
    }

    public IDisposable EnterMirrorDownloadScope(HosterRegistration registration)
    {
        var globalMegabytesPerSecond = configurationProvider.GetValue<DownloadConfiguration>(
            configuration => configuration.DownloadSpeedLimitMegabytesPerSecond
        );

        return EnterScope(
            speedLimiters: mirrorDownloadSpeedLimiters,
            hosterRegistrationId: registration.Id,
            hosterRegistrationMegabytesPerSecond: registration.MirrorDownloadSpeedLimitMegabytesPerSecond,
            globalMegabytesPerSecond: globalMegabytesPerSecond
        );
    }

    public void Dispose()
    {
        lock (limiterUpdateLock)
        {
            uploadSpeedLimiters.DisposeAll();
            mirrorDownloadSpeedLimiters.DisposeAll();
        }
    }

    private IDisposable EnterScope(
        SpeedLimitersForTransferDirection speedLimiters,
        int hosterRegistrationId,
        decimal? hosterRegistrationMegabytesPerSecond,
        decimal? globalMegabytesPerSecond
    )
    {
        List<TransferSpeedLimiter> activeLimiters = [];

        lock (limiterUpdateLock)
        {
            var hosterRegistrationLimiter = ApplyConfiguredLimit(
                currentLimiter: speedLimiters.LimiterByHosterRegistrationId.GetValueOrDefault(
                    hosterRegistrationId
                ),
                megabytesPerSecond: hosterRegistrationMegabytesPerSecond
            );

            if (hosterRegistrationLimiter is null)
            {
                speedLimiters.LimiterByHosterRegistrationId.Remove(hosterRegistrationId);
            }
            else
            {
                speedLimiters.LimiterByHosterRegistrationId[hosterRegistrationId] =
                    hosterRegistrationLimiter;
                activeLimiters.Add(hosterRegistrationLimiter);
            }

            speedLimiters.GlobalLimiter = ApplyConfiguredLimit(
                currentLimiter: speedLimiters.GlobalLimiter,
                megabytesPerSecond: globalMegabytesPerSecond
            );

            if (speedLimiters.GlobalLimiter is not null)
            {
                activeLimiters.Add(speedLimiters.GlobalLimiter);
            }
        }

        return TransferSpeedLimitScope.Enter(activeLimiters);
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

    private sealed class SpeedLimitersForTransferDirection
    {
        public Dictionary<int, TransferSpeedLimiter> LimiterByHosterRegistrationId { get; } = [];

        public TransferSpeedLimiter? GlobalLimiter { get; set; }

        public void DisposeAll()
        {
            foreach (var limiter in LimiterByHosterRegistrationId.Values)
            {
                limiter.Dispose();
            }

            LimiterByHosterRegistrationId.Clear();
            GlobalLimiter?.Dispose();
            GlobalLimiter = null;
        }
    }
}
