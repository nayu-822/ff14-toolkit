using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Models.OverlayPlugin;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;

namespace FF14Toolkit.App.Services.OverlayPlugin;

public sealed class OverlayPluginWebSocketService : IOverlayPluginWebSocketService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly TimeSpan requestTimeout;
    private readonly Uri webSocketUri;
    private long sequenceNumber;

    public OverlayPluginWebSocketService(IOptions<OverlayPluginOptions> overlayPluginOptions)
    {
        if (overlayPluginOptions is null)
        {
            throw new ArgumentNullException(nameof(overlayPluginOptions));
        }

        OverlayPluginOptions options = overlayPluginOptions.Value;
        webSocketUri = new Uri(options.WebSocketUri, UriKind.Absolute);
        requestTimeout = TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds));
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using ClientWebSocket webSocket = await ConnectAsync(cancellationToken);
            return webSocket.State == WebSocketState.Open;
        }
        catch
        {
            return false;
        }
    }

    public async Task<JsonDocument> CallAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(call);

        using ClientWebSocket webSocket = await ConnectAsync(cancellationToken);
        long sequence = Interlocked.Increment(ref sequenceNumber);

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
                payload[key] = CreateNode(value);
            }
        }

        string requestJson = payload.ToJsonString();
        byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
        await webSocket.SendAsync(
            requestBytes,
            WebSocketMessageType.Text,
            true,
            cancellationToken);

        string responseJson = await ReceiveMessageAsync(webSocket, cancellationToken);
        JsonDocument document = JsonDocument.Parse(responseJson);

        if (document.RootElement.TryGetProperty("rseq", out JsonElement responseSequenceElement)
            && responseSequenceElement.ValueKind == JsonValueKind.Number
            && responseSequenceElement.TryGetInt64(out long responseSequence)
            && responseSequence != sequence)
        {
            document.Dispose();
            throw new InvalidOperationException("OverlayPlugin WebSocket response sequence did not match the request.");
        }

        return document;
    }

    public async Task<OverlayPluginLanguageInfo?> GetLanguageAsync(CancellationToken cancellationToken = default)
    {
        return await CallAndDeserializeAsync<OverlayPluginLanguageInfo>("getLanguage", cancellationToken: cancellationToken);
    }

    public async Task<OverlayPluginVersionInfo?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        return await CallAndDeserializeAsync<OverlayPluginVersionInfo>("getVersion", cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<OverlayPluginCombatant>> GetCombatantsAsync(CancellationToken cancellationToken = default)
    {
        OverlayPluginCombatantsResponse? response = await CallAndDeserializeAsync<OverlayPluginCombatantsResponse>(
            "getCombatants",
            cancellationToken: cancellationToken);
        return response?.Combatants ?? [];
    }

    private async Task<TResponse?> CallAndDeserializeAsync<TResponse>(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using JsonDocument response = await CallAsync(call, parameters, cancellationToken);
        return response.Deserialize<TResponse>(SerializerOptions);
    }

    private async Task<ClientWebSocket> ConnectAsync(CancellationToken cancellationToken)
    {
        ClientWebSocket webSocket = new();
        CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(requestTimeout);

        try
        {
            await webSocket.ConnectAsync(webSocketUri, timeoutSource.Token);
            return webSocket;
        }
        catch
        {
            webSocket.Dispose();
            throw;
        }
        finally
        {
            timeoutSource.Dispose();
        }
    }

    private async Task<string> ReceiveMessageAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(requestTimeout);

        byte[] buffer = new byte[8192];
        ArrayBufferWriter<byte> writer = new();

        while (true)
        {
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, timeoutSource.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("OverlayPlugin WebSocket closed before a response was received.");
            }

            writer.Write(buffer.AsSpan(0, result.Count));
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private static JsonNode? CreateNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToNode(value, SerializerOptions);
    }
}
