using System.Diagnostics;
using Bearcat.Abstractions.RemoteSource;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;

public sealed class RemoteSourceSessionPool(
    ILogger<RemoteSourceSessionPool> logger,
    TimeSpan idleTimeout
) : IAsyncDisposable
{
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);

    private readonly Lock gate = new();

    private readonly Dictionary<int, RegistrationSessions> sessionsByRegistrationId = new();

    private bool disposed;

    public RemoteSourceSessionPool(ILogger<RemoteSourceSessionPool> logger)
        : this(logger, DefaultIdleTimeout) { }

    public async Task<T> UseSessionAsync<T>(
        int registrationId,
        int maxConnections,
        Func<CancellationToken, Task<IRemoteSourceSession>> openSession,
        Func<IRemoteSourceSession, Task<T>> action,
        CancellationToken cancellationToken
    )
    {
        var sessions = await GetSessionsAsync(registrationId, maxConnections);
        await sessions.FreeSlots.WaitAsync(cancellationToken);

        try
        {
            var session =
                await TakeIdleSessionAsync(sessions) ?? await openSession(cancellationToken);

            try
            {
                var result = await action(session);
                await ReturnAsync(registrationId, sessions, session);

                return result;
            }
            catch
            {
                await CloseAsync([session]);
                throw;
            }
        }
        finally
        {
            sessions.FreeSlots.Release();
        }
    }

    public async Task CloseSessionsAsync(int registrationId)
    {
        List<IRemoteSourceSession> idleSessions = [];

        lock (gate)
        {
            if (sessionsByRegistrationId.Remove(registrationId, out var sessions))
            {
                idleSessions = TakeIdleSessions(sessions, _ => true);
            }
        }

        await CloseAsync(idleSessions);
    }

    public async ValueTask DisposeAsync()
    {
        List<IRemoteSourceSession> idleSessions;

        lock (gate)
        {
            disposed = true;
            idleSessions = sessionsByRegistrationId
                .Values.SelectMany(sessions => TakeIdleSessions(sessions, _ => true))
                .ToList();
        }

        await CloseAsync(idleSessions);
    }

    private async Task<RegistrationSessions> GetSessionsAsync(
        int registrationId,
        int maxConnections
    )
    {
        RegistrationSessions sessions;
        List<IRemoteSourceSession> replacedIdleSessions = [];

        lock (gate)
        {
            if (sessionsByRegistrationId.TryGetValue(registrationId, out var current))
            {
                if (current.MaxConnections == maxConnections)
                {
                    return current;
                }

                replacedIdleSessions = TakeIdleSessions(current, _ => true);
            }

            sessions = new RegistrationSessions(maxConnections);
            sessionsByRegistrationId[registrationId] = sessions;
        }

        await CloseAsync(replacedIdleSessions);

        return sessions;
    }

    private async Task<IRemoteSourceSession?> TakeIdleSessionAsync(RegistrationSessions sessions)
    {
        List<IRemoteSourceSession> expiredSessions;
        IRemoteSourceSession? newestSession = null;

        lock (gate)
        {
            expiredSessions = TakeIdleSessions(
                sessions,
                idleSession => Stopwatch.GetElapsedTime(idleSession.ReturnedAt) >= idleTimeout
            );

            if (sessions.IdleSessions.Count > 0)
            {
                newestSession = sessions.IdleSessions[^1].Session;
                sessions.IdleSessions.RemoveAt(sessions.IdleSessions.Count - 1);
            }
        }

        await CloseAsync(expiredSessions);

        return newestSession;
    }

    private async Task ReturnAsync(
        int registrationId,
        RegistrationSessions sessions,
        IRemoteSourceSession session
    )
    {
        lock (gate)
        {
            if (!disposed && sessionsByRegistrationId.GetValueOrDefault(registrationId) == sessions)
            {
                sessions.IdleSessions.Add(new IdleSession(session, Stopwatch.GetTimestamp()));

                return;
            }
        }

        await CloseAsync([session]);
    }

    private static List<IRemoteSourceSession> TakeIdleSessions(
        RegistrationSessions sessions,
        Predicate<IdleSession> match
    )
    {
        var taken = sessions.IdleSessions.FindAll(match);
        sessions.IdleSessions.RemoveAll(taken.Contains);

        return taken.ConvertAll(idleSession => idleSession.Session);
    }

    private async Task CloseAsync(List<IRemoteSourceSession> sessions)
    {
        foreach (var session in sessions)
        {
            try
            {
                await session.DisposeAsync();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Closing a remote source session failed");
            }
        }
    }

    private sealed class RegistrationSessions(int maxConnections)
    {
        public int MaxConnections { get; } = maxConnections;

        public SemaphoreSlim FreeSlots { get; } = new(maxConnections);

        public List<IdleSession> IdleSessions { get; } = [];
    }

    private sealed record IdleSession(IRemoteSourceSession Session, long ReturnedAt);
}
