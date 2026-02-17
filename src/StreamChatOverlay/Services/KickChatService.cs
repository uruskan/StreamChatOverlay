using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Services;

public sealed partial class KickChatService : IChatService
{
    private const string PusherAppKey = "32cbd69e4b950bf97679";
    private static readonly Uri PusherUri = new(
        $"wss://ws-us2.pusher.com/app/{PusherAppKey}?protocol=7&client=js&version=8.4.0-rc2&flash=false");

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    public event Action<ChatMessage>? OnMessageReceived;
    public event Action<string>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public async Task ConnectAsync(string username, CancellationToken ct = default)
    {
        // 1. Resolve chatroom ID
        int chatroomId = await ResolveChatroomIdAsync(username, ct);

        // 2. Open WebSocket
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(PusherUri, _cts.Token);

        // 3. Receive connection_established
        var connMsg = await ReceiveOneAsync(_cts.Token);
        var connEvent = JsonSerializer.Deserialize<PusherEvent>(connMsg);
        if (connEvent?.Event != "pusher:connection_established")
            throw new InvalidOperationException($"Expected connection_established, got: {connEvent?.Event}");

        // 4. Subscribe to chatroom
        var subscribePayload = JsonSerializer.Serialize(new
        {
            @event = "pusher:subscribe",
            data = new { auth = "", channel = $"chatrooms.{chatroomId}.v2" }
        });
        await SendAsync(subscribePayload, _cts.Token);

        // 5. Wait for subscription_succeeded
        var subMsg = await ReceiveOneAsync(_cts.Token);

        OnConnected?.Invoke();

        // 6. Start receive loop and ping loop
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        _ = Task.Run(() => PingLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task<int> ResolveChatroomIdAsync(string username, CancellationToken ct)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        http.DefaultRequestHeaders.Add("Referer", "https://kick.com/");

        var response = await http.GetStringAsync(
            $"https://kick.com/api/v2/channels/{username}", ct);

        using var doc = JsonDocument.Parse(response);
        return doc.RootElement.GetProperty("chatroom").GetProperty("id").GetInt32();
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnDisconnected?.Invoke();
                    return;
                }

                var raw = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessPusherMessage(raw);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
        OnDisconnected?.Invoke();
    }

    private void ProcessPusherMessage(string raw)
    {
        var envelope = JsonSerializer.Deserialize<PusherEvent>(raw);
        if (envelope == null) return;

        if (envelope.Event == "pusher:ping")
        {
            _ = SendAsync("""{"event":"pusher:pong","data":{}}""", _cts?.Token ?? default);
            return;
        }

        if (envelope.Event == "App\\Events\\ChatMessageEvent" && envelope.Data != null)
        {
            var msg = ParseChatMessageJson(envelope.Data);
            if (msg != null)
                OnMessageReceived?.Invoke(msg);
        }
    }

    public static ChatMessage? ParseChatMessageJson(string json)
    {
        var raw = JsonSerializer.Deserialize<KickChatMessageRaw>(json);
        if (raw?.Sender == null) return null;

        var fragments = ParseContent(raw.Content);
        var color = raw.Sender.Identity?.Color ?? "#53FC18"; // Kick green default

        return new ChatMessage
        {
            Id = raw.Id,
            Platform = ChatPlatform.Kick,
            Username = raw.Sender.Username,
            UsernameColor = color,
            Fragments = fragments,
            BadgeUrls = [], // Badges implemented later
            Timestamp = raw.CreatedAt
        };
    }

    public static List<MessageFragment> ParseContent(string content)
    {
        var fragments = new List<MessageFragment>();
        var matches = EmoteRegex().Matches(content);

        if (matches.Count == 0)
            return [MessageFragment.Text(content)];

        int lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
                fragments.Add(MessageFragment.Text(content[lastIndex..match.Index]));

            var emoteId = match.Groups[1].Value;
            var emoteName = match.Groups[2].Value;
            var url = $"https://files.kick.com/emotes/{emoteId}/fullsize";
            fragments.Add(MessageFragment.Emote(emoteName, url));

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < content.Length)
            fragments.Add(MessageFragment.Text(content[lastIndex..]));

        return fragments;
    }

    [GeneratedRegex(@"\[emote:(\d+):([^\]]+)\]")]
    private static partial Regex EmoteRegex();

    private async Task PingLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(60), ct);
                await SendAsync("""{"event":"pusher:ping","data":{}}""", ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SendAsync(string message, CancellationToken ct)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private async Task<string> ReceiveOneAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var result = await _ws!.ReceiveAsync(buffer, ct);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _ws?.Dispose();
        _cts?.Dispose();
    }

    // Pusher protocol DTOs
    private sealed class PusherEvent
    {
        [JsonPropertyName("event")] public string Event { get; set; } = "";
        [JsonPropertyName("data")] public string? Data { get; set; }
        [JsonPropertyName("channel")] public string? Channel { get; set; }
    }

    private sealed class KickChatMessageRaw
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("chatroom_id")] public int ChatroomId { get; set; }
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("sender")] public KickSenderRaw? Sender { get; set; }
    }

    private sealed class KickSenderRaw
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("slug")] public string Slug { get; set; } = "";
        [JsonPropertyName("identity")] public KickIdentityRaw? Identity { get; set; }
    }

    private sealed class KickIdentityRaw
    {
        [JsonPropertyName("color")] public string Color { get; set; } = "#FFFFFF";
        [JsonPropertyName("badges")] public List<KickBadgeRaw> Badges { get; set; } = [];
    }

    private sealed class KickBadgeRaw
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("count")] public int? Count { get; set; }
    }
}
