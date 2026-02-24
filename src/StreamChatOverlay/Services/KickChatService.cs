using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using KickLib.Client;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Services;

public sealed partial class KickChatService : IChatService
{
    private KickClient? _kickClient;

    public event Action<ChatMessage>? OnMessageReceived;
    public event Action<string>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public async Task ConnectAsync(string username, CancellationToken ct = default)
    {
        await ConnectAsync(username, null, ct);
    }

    public async Task ConnectAsync(string username, string? manualChatroomId, CancellationToken ct = default)
    {
        // 1. Resolve chatroom ID (use manual override if provided)
        int chatroomId;
        if (!string.IsNullOrWhiteSpace(manualChatroomId) && int.TryParse(manualChatroomId.Trim(), out var manualId))
        {
            chatroomId = manualId;
        }
        else
        {
            chatroomId = await ResolveChatroomIdAsync(username, ct);
        }

        // 2. Create KickLib client and wire up events
        _kickClient = new KickClient();

        _kickClient.OnConnected += (_, _) => OnConnected?.Invoke();
        _kickClient.OnDisconnected += (_, _) => OnDisconnected?.Invoke();
        _kickClient.OnMessage += (_, e) =>
        {
            try
            {
                var data = e.Data;
                if (data?.Sender == null) return;

                var fragments = ParseContent(data.Content);
                var color = data.Sender.Identity?.Color ?? "#53FC18";

                var msg = new ChatMessage
                {
                    Id = data.Id,
                    Platform = ChatPlatform.Kick,
                    Username = data.Sender.Username,
                    UsernameColor = color,
                    Fragments = fragments,
                    BadgeUrls = [],
                    Timestamp = data.CreatedAt
                };
                OnMessageReceived?.Invoke(msg);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Message parse error: {ex.Message}");
            }
        };

        // 3. Subscribe to chatroom and connect
        await _kickClient.ListenToChatRoomAsync(chatroomId);
        await _kickClient.ConnectAsync();
    }

    // --- Chatroom ID resolution (lightweight HTTP, no Puppeteer) ---

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        http.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        http.DefaultRequestHeaders.Add("Referer", "https://kick.com/");
        http.DefaultRequestHeaders.Add("Origin", "https://kick.com");
        http.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
        http.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        http.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        http.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
        http.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
        http.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");

        return http;
    }

    private async Task<int> ResolveChatroomIdAsync(string username, CancellationToken ct)
    {
        using var http = CreateHttpClient();

        string[] endpoints = [
            $"https://kick.com/api/v2/channels/{username}",
            $"https://kick.com/api/v1/channels/{username}"
        ];

        Exception? lastException = null;
        foreach (var url in endpoints)
        {
            try
            {
                var response = await http.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(ct);

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("chatroom", out var chatroom) &&
                    chatroom.TryGetProperty("id", out var id))
                    return id.GetInt32();
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(500, ct);
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve Kick chatroom ID for '{username}'. " +
            $"Cloudflare may be blocking the request. " +
            $"Enter your chatroom ID manually in Settings. " +
            $"(Error: {lastException?.Message})");
    }

    // --- Kick message content parsing ---

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

    // --- Lifecycle ---

    public async Task DisconnectAsync()
    {
        if (_kickClient != null)
            await _kickClient.DisconnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
