using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageRemoteSources.Sessions;

public class RemoteSourceSessionPoolTest
{
    private const int RegistrationId = 1;

    private static readonly TimeSpan PendingCheckDelay = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

    private ManualClock clock = null!;
    private RemoteSourceSessionPool pool = null!;
    private List<TestSession> openedSessions = null!;
    private Exception? openException;
    private readonly Lock openGate = new();

    [SetUp]
    public void Setup()
    {
        clock = new ManualClock();
        pool = new RemoteSourceSessionPool(clock, NullLogger<RemoteSourceSessionPool>.Instance);
        openedSessions = [];
        openException = null;
    }

    [TearDown]
    public async Task DisposePoolAsync()
    {
        await pool.DisposeAsync();
    }

    [Test]
    public async Task UseSessionAsync_SessionIdleWithinIdleTimeout_ReusesSession()
    {
        // Arrange
        var first = await UseSessionAsync(maxConnections: 1);
        clock.Advance(RemoteSourceSessionPool.IdleTimeout - TimeSpan.FromSeconds(1));

        // Act
        var second = await UseSessionAsync(maxConnections: 1);

        // Assert
        second.ShouldBeSameAs(first);
        openedSessions.Count.ShouldBe(1);
        openedSessions[0].IsDisposed.ShouldBeFalse();
    }

    [Test]
    public async Task UseSessionAsync_SessionIdleForIdleTimeout_ClosesItAndOpensNewSession()
    {
        // Arrange
        var first = await UseSessionAsync(maxConnections: 1);
        clock.Advance(RemoteSourceSessionPool.IdleTimeout);

        // Act
        var second = await UseSessionAsync(maxConnections: 1);

        // Assert
        second.ShouldNotBeSameAs(first);
        openedSessions.Count.ShouldBe(2);
        openedSessions[0].IsDisposed.ShouldBeTrue();
        openedSessions[1].IsDisposed.ShouldBeFalse();
    }

    [Test]
    public async Task UseSessionAsync_LimitReached_WaitsUntilASessionIsFreeAndReusesIt()
    {
        // Arrange
        var finishFirst = CreateSignal();
        var finishSecond = CreateSignal();
        var first = UseSessionAsync(maxConnections: 2, finishFirst.Task);
        var second = UseSessionAsync(maxConnections: 2, finishSecond.Task);

        // Act
        var third = UseSessionAsync(maxConnections: 2);
        await Task.Delay(PendingCheckDelay);
        var thirdCompletedAtLimit = third.IsCompleted;
        finishFirst.SetResult();
        var firstSession = await first;
        var thirdSession = await third.WaitAsync(CompletionTimeout);
        finishSecond.SetResult();
        await second;

        // Assert
        thirdCompletedAtLimit.ShouldBeFalse();
        openedSessions.Count.ShouldBe(2);
        thirdSession.ShouldBeSameAs(firstSession);
    }

    [Test]
    public async Task UseSessionAsync_ActionThrows_ClosesSessionAndNextCallOpensNewSession()
    {
        // Arrange
        await Should.ThrowAsync<IOException>(() =>
            pool.UseSessionAsync<bool>(
                RegistrationId,
                1,
                OpenSessionAsync,
                _ => throw new IOException("Connection lost"),
                CancellationToken.None
            )
        );

        // Act
        var next = await UseSessionAsync(maxConnections: 1).WaitAsync(CompletionTimeout);

        // Assert
        openedSessions[0].IsDisposed.ShouldBeTrue();
        next.ShouldBeSameAs(openedSessions[1]);
    }

    [Test]
    public async Task UseSessionAsync_OpeningFails_FreesSlot()
    {
        // Arrange
        openException = new IOException("Login incorrect");
        await Should.ThrowAsync<IOException>(() => UseSessionAsync(maxConnections: 1));
        openException = null;

        // Act
        var session = await UseSessionAsync(maxConnections: 1).WaitAsync(CompletionTimeout);

        // Assert
        session.ShouldBeSameAs(openedSessions.Single());
    }

    [Test]
    public async Task UseSessionAsync_WaitingIsCanceled_ThrowsAndLaterCallsStillWork()
    {
        // Arrange
        var finishHeld = CreateSignal();
        var held = UseSessionAsync(maxConnections: 1, finishHeld.Task);
        using var cancellationSource = new CancellationTokenSource();
        var waiting = UseSessionAsync(
            maxConnections: 1,
            cancellationToken: cancellationSource.Token
        );

        // Act
        await cancellationSource.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => waiting);
        finishHeld.SetResult();
        var heldSession = await held;
        var next = await UseSessionAsync(maxConnections: 1).WaitAsync(CompletionTimeout);

        // Assert
        next.ShouldBeSameAs(heldSession);
        openedSessions.Count.ShouldBe(1);
    }

    [Test]
    public async Task UseSessionAsync_MaxConnectionsChanged_ClosesIdleSessionAndOpensNewOne()
    {
        // Arrange
        await UseSessionAsync(maxConnections: 1);

        // Act
        var next = await UseSessionAsync(maxConnections: 2);

        // Assert
        openedSessions[0].IsDisposed.ShouldBeTrue();
        next.ShouldBeSameAs(openedSessions[1]);
    }

    [Test]
    public async Task UseSessionAsync_MaxConnectionsRaisedWhileSessionIsBusy_DoesNotWaitAndClosesOldSessionAfterwards()
    {
        // Arrange
        var finishHeld = CreateSignal();
        var held = UseSessionAsync(maxConnections: 1, finishHeld.Task);

        // Act
        var raised = await UseSessionAsync(maxConnections: 2).WaitAsync(CompletionTimeout);
        finishHeld.SetResult();
        await held;

        // Assert
        raised.ShouldBeSameAs(openedSessions[1]);
        openedSessions[0].IsDisposed.ShouldBeTrue();
        openedSessions[1].IsDisposed.ShouldBeFalse();
    }

    [Test]
    public async Task CloseSessionsAsync_IdleAndBusySessions_ClosesIdleNowAndBusyWhenFinished()
    {
        // Arrange
        var finishBusy = CreateSignal();
        var busy = UseSessionAsync(maxConnections: 2, finishBusy.Task);
        await UseSessionAsync(maxConnections: 2);

        // Act
        await pool.CloseSessionsAsync(RegistrationId);
        var busyClosedWhileRunning = openedSessions[0].IsDisposed;
        finishBusy.SetResult();
        await busy;
        var next = await UseSessionAsync(maxConnections: 2);

        // Assert
        busyClosedWhileRunning.ShouldBeFalse();
        openedSessions[0].IsDisposed.ShouldBeTrue();
        openedSessions[1].IsDisposed.ShouldBeTrue();
        next.ShouldBeSameAs(openedSessions[2]);
    }

    [Test]
    public async Task DisposeAsync_IdleSession_IsClosed()
    {
        // Arrange
        await UseSessionAsync(maxConnections: 1);

        // Act
        await pool.DisposeAsync();

        // Assert
        openedSessions.Single().IsDisposed.ShouldBeTrue();
    }

    private Task<IRemoteSourceSession> UseSessionAsync(
        int maxConnections,
        Task? finished = null,
        CancellationToken cancellationToken = default
    )
    {
        return pool.UseSessionAsync(
            RegistrationId,
            maxConnections,
            OpenSessionAsync,
            async session =>
            {
                await (finished ?? Task.CompletedTask);

                return session;
            },
            cancellationToken
        );
    }

    private static TaskCompletionSource CreateSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private Task<IRemoteSourceSession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        if (openException is not null)
        {
            throw openException;
        }

        var session = new TestSession();

        lock (openGate)
        {
            openedSessions.Add(session);
        }

        return Task.FromResult<IRemoteSourceSession>(session);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return timestamp;
        }

        public void Advance(TimeSpan duration)
        {
            timestamp += duration.Ticks;
        }
    }

    private sealed class TestSession : IRemoteSourceSession
    {
        public bool IsDisposed { get; private set; }

        public Task<IReadOnlyList<RemoteFolderDto>> ListFoldersAsync(
            string path,
            CancellationToken cancellationToken
        )
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<RemoteFileDto>> ListFilesRecursiveAsync(
            string folderPath,
            CancellationToken cancellationToken
        )
        {
            throw new NotSupportedException();
        }

        public Task DownloadFileAsync(
            RemoteFileDto file,
            string localFilePath,
            ITransferProgress progress,
            CancellationToken cancellationToken
        )
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
