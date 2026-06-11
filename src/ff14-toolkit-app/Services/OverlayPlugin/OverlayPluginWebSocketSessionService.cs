using FF14Toolkit.App.Models.Configuration;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FF14Toolkit.App.Services.OverlayPlugin;

public sealed class OverlayPluginWebSocketSessionService : IOverlayPluginWebSocketSessionService, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<string>> pendingRequests = new();
    private readonly TimeSpan requestTimeout;
    private readonly Uri webSocketUri;
    private readonly HashSet<string> subscribedEvents = new(StringComparer.Ordinal);
    private ClientWebSocket? webSocket;
    private CancellationTokenSource? receiveLoopCancellationTokenSource;
    private Task? receiveLoopTask;
    private long sequenceNumber;

    public OverlayPluginWebSocketSessionService(IOptions<OverlayPluginOptions> overlayPluginOptions)
    {
        if (overlayPluginOptions is null)
        {
            throw new ArgumentNullException(nameof(overlayPluginOptions));
        }

        OverlayPluginOptions options = overlayPluginOptions.Value;
        webSocketUri = new Uri(options.WebSocketUri, UriKind.Absolute);
        requestTimeout = TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds));
    }

    public event EventHandler<OverlayPluginEventReceivedEventArgs>? EventReceived;
    public event EventHandler? ConnectionStateChanged;

    public bool IsStarted => webSocket?.State == WebSocketState.Open;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (webSocket?.State == WebSocketState.Open)
            {
                return;
            }

            await DisposeCurrentSocketAsync(cancellationToken);

            ClientWebSocket nextWebSocket = new();
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(requestTimeout);

            try
            {
                await nextWebSocket.ConnectAsync(webSocketUri, timeoutSource.Token);
                webSocket = nextWebSocket;
                receiveLoopCancellationTokenSource = new CancellationTokenSource();
                receiveLoopTask = Task.Run(() => ReceiveLoopAsync(nextWebSocket, receiveLoopCancellationTokenSource.Token));
                OnConnectionStateChanged();
            }
            catch
            {
                nextWebSocket.Dispose();
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await DisposeCurrentSocketAsync(cancellationToken);
            OnConnectionStateChanged();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SubscribeAsync(IEnumerable<string> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (webSocket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("OverlayPlugin WSServer is not started.");
            }

            foreach (string eventName in events.Where(static item => !string.IsNullOrWhiteSpace(item)))
            {
                subscribedEvents.Add(eventName);
            }

            JsonObject payload = new()
            {
                ["call"] = "subscribe",
                ["events"] = JsonSerializer.SerializeToNode(subscribedEvents.OrderBy(static item => item).ToArray())
            };

            await SendJsonAsync(payload.ToJsonString(), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> SendRequestAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(call);

        ClientWebSocket currentWebSocket;
        long sequence = 0;
        TaskCompletionSource<string> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await gate.WaitAsync(cancellationToken);
        try
        {
            currentWebSocket = webSocket?.State == WebSocketState.Open
                ? webSocket
                : throw new InvalidOperationException("OverlayPlugin WSServer is not started.");

            sequence = Interlocked.Increment(ref sequenceNumber);
            pendingRequests[sequence] = completionSource;

            JsonObject payload = new()
            {
                ["type"] = "request",
                ["call"] = call,
                ["rseq"] = sequence
            };

            if (parameters is not null)
            {
                foreach ((string key, object? value) in parameters)
                {
                    payload[key] = value is null ? null : JsonSerializer.SerializeToNode(value);
                }
            }

            await SendJsonAsync(payload.ToJsonString(), cancellationToken);
        }
        catch
        {
            if (sequence > 0)
            {
                pendingRequests.TryRemove(sequence, out _);
            }

            throw;
        }
        finally
        {
            gate.Release();
        }

        Task completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(requestTimeout, cancellationToken));
        if (completedTask != completionSource.Task)
        {
            pendingRequests.TryRemove(sequence, out _);
            throw new TimeoutException($"No response was received for '{call}' within {requestTimeout.TotalSeconds:0} seconds.");
        }

        return await completionSource.Task;
    }

    public void Dispose()
    {
        sendGate.Dispose();
        gate.Dispose();
        receiveLoopCancellationTokenSource?.Cancel();
        receiveLoopCancellationTokenSource?.Dispose();
        webSocket?.Dispose();
    }

    private async Task SendJsonAsync(string json, CancellationToken cancellationToken)
    {
        if (webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("OverlayPlugin WSServer is not started.");
        }

        byte[] requestBytes = Encoding.UTF8.GetBytes(json);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(requestTimeout);

        await sendGate.WaitAsync(timeoutSource.Token);
        try
        {
            await webSocket.SendAsync(
                requestBytes,
                WebSocketMessageType.Text,
                true,
                timeoutSource.Token);
        }
        finally
        {
            sendGate.Release();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket currentWebSocket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested && currentWebSocket.State == WebSocketState.Open)
            {
                ArrayBufferWriter<byte> writer = new();
                WebSocketReceiveResult result;

                do
                {
                    result = await currentWebSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    writer.Write(buffer.AsSpan(0, result.Count));
                }
                while (!result.EndOfMessage);

                string rawJson = Encoding.UTF8.GetString(writer.WrittenSpan);
                DispatchMessage(rawJson);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            FailPendingRequests("OverlayPlugin WSServer connection was closed.");
            if (ReferenceEquals(webSocket, currentWebSocket))
            {
                webSocket = null;
                OnConnectionStateChanged();
            }
        }
    }

    private void DispatchMessage(string rawJson)
    {
        using JsonDocument document = JsonDocument.Parse(rawJson);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("rseq", out JsonElement responseSequenceElement)
            && responseSequenceElement.ValueKind == JsonValueKind.Number
            && responseSequenceElement.TryGetInt64(out long responseSequence)
            && pendingRequests.TryRemove(responseSequence, out TaskCompletionSource<string>? completionSource))
        {
            completionSource.TrySetResult(rawJson);
            return;
        }

        string eventType = root.TryGetProperty("type", out JsonElement typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? "Unknown"
            : "Unknown";

        EventReceived?.Invoke(this, new OverlayPluginEventReceivedEventArgs
        {
            EventType = eventType,
            RawJson = rawJson
        });
    }

    private async Task DisposeCurrentSocketAsync(CancellationToken cancellationToken)
    {
        receiveLoopCancellationTokenSource?.Cancel();

        if (receiveLoopTask is not null)
        {
            try
            {
                await receiveLoopTask;
            }
            catch
            {
            }
        }

        receiveLoopTask = null;
        receiveLoopCancellationTokenSource?.Dispose();
        receiveLoopCancellationTokenSource = null;

        if (webSocket is null)
        {
            return;
        }

        ClientWebSocket currentWebSocket = webSocket;
        webSocket = null;
        OnConnectionStateChanged();

        if (currentWebSocket.State == WebSocketState.Open)
        {
            try
            {
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(2));
                await currentWebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "session closed",
                    timeoutSource.Token);
            }
            catch
            {
            }
        }

        currentWebSocket.Dispose();
        FailPendingRequests("OverlayPlugin WSServer connection was closed.");
    }

    private void OnConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FailPendingRequests(string message)
    {
        foreach ((long sequence, TaskCompletionSource<string> completionSource) in pendingRequests.ToArray())
        {
            if (pendingRequests.TryRemove(sequence, out _))
            {
                completionSource.TrySetException(new InvalidOperationException(message));
            }
        }
    }
}
