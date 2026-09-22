using System.Globalization;
using Bearcat.Infrastructure.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.Logs;

public sealed partial class LogsPage(LogStreamBroadcaster broadcaster, IJSRuntime jsRuntime)
    : IDisposable
{
    private const int MaxDisplayedLines = 1000;

    private readonly List<LogLine> lines = [];
    private readonly CancellationTokenSource lifetimeCancellation = new();

    private ElementReference logContainer;
    private LogStreamSubscription? subscription;
    private CancellationTokenSource? streamCancellation;
    private bool isStreaming;
    private bool isAttached;
    private bool hasNewLines;

    protected override void OnInitialized()
    {
        LoadSnapshot();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await jsRuntime.InvokeVoidAsync("bearcat.logView.attach", logContainer);
            isAttached = true;
            StartStreaming();
            StateHasChanged();
            return;
        }

        if (!isAttached || !hasNewLines)
        {
            return;
        }

        hasNewLines = false;
        await jsRuntime.InvokeVoidAsync("bearcat.logView.scrollToBottomIfPinned", logContainer);
    }

    private void ToggleStreaming()
    {
        if (isStreaming)
        {
            PauseStreaming();
            return;
        }

        StartStreaming();
    }

    private void StartStreaming()
    {
        LoadSnapshot();

        subscription = broadcaster.Subscribe();
        streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellation.Token
        );
        isStreaming = true;
        hasNewLines = true;

        _ = ReadStreamAsync(subscription, streamCancellation.Token);
    }

    private void LoadSnapshot()
    {
        lines.Clear();
        lines.AddRange(broadcaster.GetSnapshot());
        TrimLines();
    }

    private void PauseStreaming()
    {
        isStreaming = false;

        streamCancellation?.Cancel();
        streamCancellation?.Dispose();
        streamCancellation = null;

        subscription?.Dispose();
        subscription = null;
    }

    private async Task ReadStreamAsync(
        LogStreamSubscription activeSubscription,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await foreach (var line in activeSubscription.Reader.ReadAllAsync(cancellationToken))
            {
                var batch = new List<LogLine> { line };

                while (activeSubscription.Reader.TryRead(out var pendingLine))
                {
                    batch.Add(pendingLine);
                }

                await InvokeAsync(() => AppendLines(batch));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void AppendLines(List<LogLine> batch)
    {
        lines.AddRange(batch);
        TrimLines();
        hasNewLines = true;
        StateHasChanged();
    }

    private void ClearLines()
    {
        lines.Clear();
    }

    private void TrimLines()
    {
        if (lines.Count <= MaxDisplayedLines)
        {
            return;
        }

        lines.RemoveRange(0, lines.Count - MaxDisplayedLines);
    }

    private static string FormatTimestamp(LogLine line)
    {
        return line.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "OFF",
        };
    }

    private static string FormatCategory(string category)
    {
        var lastSeparator = category.LastIndexOf('.');

        return lastSeparator < 0 ? category : category[(lastSeparator + 1)..];
    }

    private static string LevelClass(LogLevel level)
    {
        return level switch
        {
            LogLevel.Critical or LogLevel.Error => "text-destructive",
            LogLevel.Warning => "text-amber-600 dark:text-amber-400",
            LogLevel.Trace or LogLevel.Debug => "text-muted-foreground",
            _ => "text-foreground",
        };
    }

    public void Dispose()
    {
        lifetimeCancellation.Cancel();
        PauseStreaming();
        lifetimeCancellation.Dispose();
    }
}
