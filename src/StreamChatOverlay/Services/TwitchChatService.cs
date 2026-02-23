using StreamChatOverlay.Models;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchChatMsg = TwitchLib.Client.Models.ChatMessage;

namespace StreamChatOverlay.Services;

public sealed class TwitchChatService : IChatService
{
    private TwitchClient? _client;
    private string? _channel;

    public event Action<Models.ChatMessage>? OnMessageReceived;
    public event Action<string>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    // Used for parsing and testing
    public record EmotePosition(string Id, int Start, int End);

    public async Task ConnectAsync(string username, CancellationToken ct = default)
    {
        // Clean up any existing connection
        if (_client != null)
        {
            _client.OnConnected -= HandleConnected;
            _client.OnDisconnected -= HandleDisconnected;
            _client.OnConnectionError -= HandleConnectionError;
            _client.OnMessageReceived -= HandleMessage;
            try { await _client.DisconnectAsync(); } catch { }
            _client = null;
        }

        _channel = username.ToLowerInvariant();

        // Anonymous credentials (justinfan) - read-only access to chat
        var credentials = new ConnectionCredentials();

        _client = new TwitchClient();
        _client.Initialize(credentials, _channel);

        _client.OnConnected += HandleConnected;
        _client.OnDisconnected += HandleDisconnected;
        _client.OnConnectionError += HandleConnectionError;
        _client.OnMessageReceived += HandleMessage;

        await _client.ConnectAsync();
    }

    private Task HandleConnected(object? sender, OnConnectedEventArgs e)
    {
        OnConnected?.Invoke();
        return Task.CompletedTask;
    }

    private Task HandleDisconnected(object? sender, OnDisconnectedArgs e)
    {
        OnDisconnected?.Invoke();
        return Task.CompletedTask;
    }

    private Task HandleConnectionError(object? sender, OnConnectionErrorArgs e)
    {
        OnError?.Invoke(e.Error.Message);
        return Task.CompletedTask;
    }

    private Task HandleMessage(object? sender, OnMessageReceivedArgs e)
    {
        TwitchChatMsg msg = e.ChatMessage;

        var emotes = msg.EmoteSet.Emotes
            .Select(em => new EmotePosition(em.Id, em.StartIndex, em.EndIndex))
            .ToList();

        var fragments = ParseFragments(msg.Message, emotes);

        var badges = msg.Badges
            .Select(b => GetTwitchBadgeUrl(b.Key, b.Value))
            .Where(url => url != null)
            .Cast<string>()
            .ToList();

        var chatMsg = new Models.ChatMessage
        {
            Id = msg.Id,
            Platform = ChatPlatform.Twitch,
            Username = msg.DisplayName,
            UsernameColor = string.IsNullOrEmpty(msg.HexColor) ? "#9147FF" : msg.HexColor,
            Fragments = fragments,
            BadgeUrls = badges,
            Timestamp = DateTime.UtcNow
        };

        OnMessageReceived?.Invoke(chatMsg);
        return Task.CompletedTask;
    }

    public static List<MessageFragment> ParseFragments(string message, List<EmotePosition> emotes)
    {
        if (emotes.Count == 0)
            return [MessageFragment.Text(message)];

        var sorted = emotes.OrderBy(e => e.Start).ToList();
        var fragments = new List<MessageFragment>();
        int lastIndex = 0;

        foreach (var emote in sorted)
        {
            if (emote.Start > lastIndex)
                fragments.Add(MessageFragment.Text(message[lastIndex..emote.Start]));

            var emoteName = message[emote.Start..(emote.End + 1)];
            var url = $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/3.0";
            fragments.Add(MessageFragment.Emote(emoteName, url));

            lastIndex = emote.End + 1;
        }

        if (lastIndex < message.Length)
            fragments.Add(MessageFragment.Text(message[lastIndex..]));

        return fragments;
    }

    private static string? GetTwitchBadgeUrl(string badgeName, string version)
    {
        // Twitch global badge CDN - simplified version
        // Full implementation would fetch badge sets from Twitch API
        return null; // Badges will be implemented in the emote resolver task
    }

    public async Task DisconnectAsync()
    {
        if (_client != null)
            await _client.DisconnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
            await _client.DisconnectAsync();
    }
}
