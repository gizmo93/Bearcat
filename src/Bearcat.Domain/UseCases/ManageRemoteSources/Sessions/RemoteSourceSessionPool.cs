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
        var sessions = await GetOrCreateRegistrationSessionsAsync(registrationId, maxConnections);
        await sessions.FreeSlots.WaitAsync(cancellationToken);

        try
        {
            var session =
                await TryReuseIdleSessionAsync(sessions) ?? await openSession(cancellationToken);

            try
            {
                var result = await action(session);
                await ReturnToIdleOrCloseAsync(registrationId, sessions, session);

                return result;
            }
            catch
            {
                await DisposeSessionsAsync([session]);
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
                idleSessions = RemoveAllIdleSessions(sessions);
            }
        }

        await DisposeSessionsAsync(idleSessions);
    }

    public async ValueTask DisposeAsync()
    {
        List<IRemoteSourceSession> idleSessions;

        lock (gate)
        {
            disposed = true;
            idleSessions = sessionsByRegistrationId
                .Values.SelectMany(sessions => RemoveAllIdleSessions(sessions))
                .ToList();
        }

        await DisposeSessionsAsync(idleSessions);
    }

    private async Task<RegistrationSessions> GetOrCreateRegistrationSessionsAsync(
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

                replacedIdleSessions = RemoveAllIdleSessions(current);
            }

            sessions = new RegistrationSessions(maxConnections);
            sessionsByRegistrationId[registrationId] = sessions;
        }

        await DisposeSessionsAsync(replacedIdleSessions);

        return sessions;
    }

    private async Task<IRemoteSourceSession?> TryReuseIdleSessionAsync(
        RegistrationSessions sessions
    )
    {
        List<IRemoteSourceSession> expiredSessions;
        IRemoteSourceSession? newestSession = null;

        lock (gate)
        {
            expiredSessions = RemoveIdleSessionsWhere(
                sessions,
                idleSession => Stopwatch.GetElapsedTime(idleSession.ReturnedAt) >= idleTimeout
            );

            if (sessions.IdleSessions.Count > 0)
            {
                newestSession = sessions.IdleSessions[^1].Session;
                sessions.IdleSessions.RemoveAt(sessions.IdleSessions.Count - 1);
            }
        }

        await DisposeSessionsAsync(expiredSessions);

        return newestSession;
    }

    private async Task ReturnToIdleOrCloseAsync(
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

        await DisposeSessionsAsync([session]);
    }

    private static List<IRemoteSourceSession> RemoveAllIdleSessions(RegistrationSessions sessions)
    {
        return RemoveIdleSessionsWhere(sessions, _ => true);
    }

    private static List<IRemoteSourceSession> RemoveIdleSessionsWhere(
        RegistrationSessions sessions,
        Predicate<IdleSession> match
    )
    {
        var removed = sessions.IdleSessions.FindAll(match);
        sessions.IdleSessions.RemoveAll(removed.Contains);

        return removed.ConvertAll(idleSession => idleSession.Session);
    }

    private async Task DisposeSessionsAsync(List<IRemoteSourceSession> sessions)
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
