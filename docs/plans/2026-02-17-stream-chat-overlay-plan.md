# Stream Chat Overlay Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a WPF desktop app that displays combined Twitch + Kick chat as a transparent, always-on-top overlay with inline emote rendering.

**Architecture:** .NET 8 WPF app using MVVM (CommunityToolkit.Mvvm). Two chat services (Twitch IRC via TwitchLib, Kick via Pusher WebSocket) emit unified ChatMessage objects into a shared ObservableCollection. Overlay window renders messages with inline emotes via TextBlock attached behavior. System tray icon provides right-click control.

**Tech Stack:** .NET 8 WPF, CommunityToolkit.Mvvm, TwitchLib.Client, H.NotifyIcon.Wpf, WpfAnimatedGif, System.Net.WebSockets, System.Text.Json

---

## Project Structure

```
StreamChatOverlay/
├── StreamChatOverlay.sln
├── src/
│   └── StreamChatOverlay/
│       ├── StreamChatOverlay.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── Models/
│       │   ├── ChatMessage.cs          # Unified message from both platforms
│       │   ├── MessageFragment.cs      # Text or emote fragment
│       │   └── AppSettings.cs          # Persisted settings
│       ├── Services/
│       │   ├── IChatService.cs         # Shared interface
│       │   ├── TwitchChatService.cs    # Twitch IRC via TwitchLib
│       │   ├── KickChatService.cs      # Kick Pusher WebSocket
│       │   └── EmoteResolver.cs        # Emote fetching + caching
│       ├── ViewModels/
│       │   ├── OverlayViewModel.cs     # Chat collection, connection logic
│       │   └── SettingsViewModel.cs    # Settings bindings
│       ├── Views/
│       │   ├── OverlayWindow.xaml      # Transparent overlay
│       │   └── SettingsWindow.xaml     # Settings panel
│       ├── Behaviors/
│       │   └── InlineMessageBehavior.cs # TextBlock inline emote renderer
│       └── Resources/
│           └── app.ico                 # App icon
└── tests/
    └── StreamChatOverlay.Tests/
        ├── StreamChatOverlay.Tests.csproj
        ├── Models/
        │   └── MessageFragmentTests.cs
        ├── Services/
        │   ├── TwitchMessageParsingTests.cs
        │   └── KickMessageParsingTests.cs
        └── TestData/
            └── (sample JSON payloads)
```

---

## Task 1: Project Scaffolding

**Files:**
- Create: `StreamChatOverlay.sln`
- Create: `src/StreamChatOverlay/StreamChatOverlay.csproj`
- Create: `tests/StreamChatOverlay.Tests/StreamChatOverlay.Tests.csproj`

**Step 1: Verify .NET 8 SDK is installed**

Run: `dotnet --list-sdks`
Expected: A line starting with `8.` (e.g., `8.0.xxx`). If not found, install from https://dotnet.microsoft.com/download/dotnet/8.0

**Step 2: Create solution and projects**

```bash
cd "C:\Users\aurus\OneDrive\Desktop\StreamChatOverlay"
dotnet new sln -n StreamChatOverlay
mkdir -p src/StreamChatOverlay
dotnet new wpf -n StreamChatOverlay -o src/StreamChatOverlay --framework net8.0
mkdir -p tests/StreamChatOverlay.Tests
dotnet new xunit -n StreamChatOverlay.Tests -o tests/StreamChatOverlay.Tests --framework net8.0
dotnet sln add src/StreamChatOverlay/StreamChatOverlay.csproj
dotnet sln add tests/StreamChatOverlay.Tests/StreamChatOverlay.Tests.csproj
dotnet add tests/StreamChatOverlay.Tests/StreamChatOverlay.Tests.csproj reference src/StreamChatOverlay/StreamChatOverlay.csproj
```

**Step 3: Install NuGet packages**

```bash
cd src/StreamChatOverlay
dotnet add package CommunityToolkit.Mvvm
dotnet add package TwitchLib.Client
dotnet add package H.NotifyIcon.Wpf
dotnet add package WpfAnimatedGif
cd ../../tests/StreamChatOverlay.Tests
dotnet add package Moq
```

**Step 4: Verify build**

Run: `dotnet build StreamChatOverlay.sln`
Expected: Build succeeded with 0 errors.

**Step 5: Create directory structure**

```bash
cd src/StreamChatOverlay
mkdir -p Models Services ViewModels Views Behaviors Resources
```

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution with WPF app and test projects"
```

---

## Task 2: Core Models

**Files:**
- Create: `src/StreamChatOverlay/Models/MessageFragment.cs`
- Create: `src/StreamChatOverlay/Models/ChatMessage.cs`
- Create: `src/StreamChatOverlay/Models/AppSettings.cs`
- Create: `src/StreamChatOverlay/Services/IChatService.cs`
- Test: `tests/StreamChatOverlay.Tests/Models/MessageFragmentTests.cs`

**Step 1: Write tests for MessageFragment parsing**

```csharp
// tests/StreamChatOverlay.Tests/Models/MessageFragmentTests.cs
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Tests.Models;

public class MessageFragmentTests
{
    [Fact]
    public void TextFragment_HasCorrectType()
    {
        var fragment = MessageFragment.Text("hello");
        Assert.Equal(FragmentType.Text, fragment.Type);
        Assert.Equal("hello", fragment.Content);
        Assert.Null(fragment.EmoteUrl);
    }

    [Fact]
    public void EmoteFragment_HasCorrectType()
    {
        var fragment = MessageFragment.Emote("Kappa", "https://cdn.example.com/kappa.png");
        Assert.Equal(FragmentType.Emote, fragment.Type);
        Assert.Equal("Kappa", fragment.Content);
        Assert.Equal("https://cdn.example.com/kappa.png", fragment.EmoteUrl);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/StreamChatOverlay.Tests --filter "MessageFragmentTests" -v n`
Expected: FAIL - types do not exist yet.

**Step 3: Implement models**

```csharp
// src/StreamChatOverlay/Models/MessageFragment.cs
namespace StreamChatOverlay.Models;

public enum FragmentType { Text, Emote }

public sealed class MessageFragment
{
    public FragmentType Type { get; }
    public string Content { get; }
    public string? EmoteUrl { get; }
    public bool IsAnimated { get; }

    private MessageFragment(FragmentType type, string content, string? emoteUrl, bool isAnimated)
    {
        Type = type;
        Content = content;
        EmoteUrl = emoteUrl;
        IsAnimated = isAnimated;
    }

    public static MessageFragment Text(string text) => new(FragmentType.Text, text, null, false);

    public static MessageFragment Emote(string name, string url, bool isAnimated = false)
        => new(FragmentType.Emote, name, url, isAnimated);
}
```

```csharp
// src/StreamChatOverlay/Models/ChatMessage.cs
namespace StreamChatOverlay.Models;

public enum ChatPlatform { Twitch, Kick }

public sealed class ChatMessage
{
    public required string Id { get; init; }
    public required ChatPlatform Platform { get; init; }
    public required string Username { get; init; }
    public required string UsernameColor { get; init; }
    public required List<MessageFragment> Fragments { get; init; }
    public required List<string> BadgeUrls { get; init; }
    public required DateTime Timestamp { get; init; }
}
```

```csharp
// src/StreamChatOverlay/Services/IChatService.cs
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Services;

public interface IChatService : IAsyncDisposable
{
    event Action<ChatMessage>? OnMessageReceived;
    event Action<string>? OnError;
    event Action? OnConnected;
    event Action? OnDisconnected;
    Task ConnectAsync(string username, CancellationToken ct = default);
    Task DisconnectAsync();
}
```

```csharp
// src/StreamChatOverlay/Models/AppSettings.cs
using System.Text.Json;

namespace StreamChatOverlay.Models;

public sealed class AppSettings
{
    public string TwitchUsername { get; set; } = "";
    public string KickUsername { get; set; } = "";
    public double FontSize { get; set; } = 14;
    public double Opacity { get; set; } = 0.75;
    public string BackgroundColor { get; set; } = "#BF000000";
    public string TextColor { get; set; } = "#FFFFFF";
    public bool ShowPlatformIcon { get; set; } = true;
    public bool ShowBadges { get; set; } = true;
    public bool ShowEmotes { get; set; } = true;
    public int MaxMessages { get; set; } = 200;
    public bool MessageFadeEnabled { get; set; } = false;
    public int MessageFadeSeconds { get; set; } = 30;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 350;
    public double WindowHeight { get; set; } = 600;

    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StreamChatOverlay.Tests --filter "MessageFragmentTests" -v n`
Expected: 2 passed, 0 failed.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add core models (ChatMessage, MessageFragment, AppSettings, IChatService)"
```

---

## Task 3: Twitch Chat Service

**Files:**
- Create: `src/StreamChatOverlay/Services/TwitchChatService.cs`
- Test: `tests/StreamChatOverlay.Tests/Services/TwitchMessageParsingTests.cs`

**Step 1: Write tests for Twitch emote parsing helper**

```csharp
// tests/StreamChatOverlay.Tests/Services/TwitchMessageParsingTests.cs
using StreamChatOverlay.Models;
using StreamChatOverlay.Services;

namespace StreamChatOverlay.Tests.Services;

public class TwitchMessageParsingTests
{
    [Fact]
    public void ParseFragments_PlainText_ReturnsSingleTextFragment()
    {
        var fragments = TwitchChatService.ParseFragments("hello world", []);
        Assert.Single(fragments);
        Assert.Equal(FragmentType.Text, fragments[0].Type);
        Assert.Equal("hello world", fragments[0].Content);
    }

    [Fact]
    public void ParseFragments_WithEmote_SplitsIntoFragments()
    {
        // "hello Kappa world" with Kappa at positions 6-10
        var emotes = new List<TwitchChatService.EmotePosition>
        {
            new("25", 6, 10) // Kappa emote ID is 25
        };

        var fragments = TwitchChatService.ParseFragments("hello Kappa world", emotes);

        Assert.Equal(3, fragments.Count);
        Assert.Equal(FragmentType.Text, fragments[0].Type);
        Assert.Equal("hello ", fragments[0].Content);
        Assert.Equal(FragmentType.Emote, fragments[1].Type);
        Assert.Equal("Kappa", fragments[1].Content);
        Assert.Contains("25", fragments[1].EmoteUrl!);
        Assert.Equal(FragmentType.Text, fragments[2].Type);
        Assert.Equal(" world", fragments[2].Content);
    }

    [Fact]
    public void ParseFragments_EmoteAtStart_NoLeadingText()
    {
        var emotes = new List<TwitchChatService.EmotePosition>
        {
            new("25", 0, 4) // Kappa
        };

        var fragments = TwitchChatService.ParseFragments("Kappa test", emotes);

        Assert.Equal(2, fragments.Count);
        Assert.Equal(FragmentType.Emote, fragments[0].Type);
        Assert.Equal(FragmentType.Text, fragments[1].Type);
        Assert.Equal(" test", fragments[1].Content);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/StreamChatOverlay.Tests --filter "TwitchMessageParsingTests" -v n`
Expected: FAIL - TwitchChatService does not exist.

**Step 3: Implement TwitchChatService**

```csharp
// src/StreamChatOverlay/Services/TwitchChatService.cs
using StreamChatOverlay.Models;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace StreamChatOverlay.Services;

public sealed class TwitchChatService : IChatService
{
    private TwitchClient? _client;
    private string? _channel;

    public event Action<ChatMessage>? OnMessageReceived;
    public event Action<string>? OnError;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    // Used for parsing and testing
    public record EmotePosition(string Id, int Start, int End);

    public async Task ConnectAsync(string username, CancellationToken ct = default)
    {
        _channel = username.ToLowerInvariant();

        var credentials = new ConnectionCredentials("justinfan12345", "");
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        var wsClient = new WebSocketClient(clientOptions);
        _client = new TwitchClient(wsClient);
        _client.Initialize(credentials, _channel);

        _client.OnConnected += (_, _) => OnConnected?.Invoke();
        _client.OnDisconnected += (_, _) => OnDisconnected?.Invoke();
        _client.OnConnectionError += (_, e) => OnError?.Invoke(e.Error.Message);
        _client.OnMessageReceived += HandleMessage;

        _client.Connect();
        await Task.CompletedTask;
    }

    private void HandleMessage(object? sender, OnMessageReceivedArgs e)
    {
        var msg = e.ChatMessage;

        var emotes = msg.EmoteSet.Emotes
            .Select(em => new EmotePosition(em.Id, em.StartIndex, em.EndIndex))
            .ToList();

        var fragments = ParseFragments(msg.Message, emotes);

        var badges = msg.Badges
            .Select(b => GetTwitchBadgeUrl(b.Key, b.Value))
            .Where(url => url != null)
            .Cast<string>()
            .ToList();

        var chatMsg = new ChatMessage
        {
            Id = msg.Id,
            Platform = ChatPlatform.Twitch,
            Username = msg.DisplayName,
            UsernameColor = string.IsNullOrEmpty(msg.ColorHex) ? "#9147FF" : msg.ColorHex,
            Fragments = fragments,
            BadgeUrls = badges,
            Timestamp = DateTime.UtcNow
        };

        OnMessageReceived?.Invoke(chatMsg);
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

    public Task DisconnectAsync()
    {
        _client?.Disconnect();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client?.Disconnect();
        return ValueTask.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StreamChatOverlay.Tests --filter "TwitchMessageParsingTests" -v n`
Expected: 3 passed, 0 failed.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add TwitchChatService with anonymous IRC connection and emote parsing"
```

---

## Task 4: Kick Chat Service

**Files:**
- Create: `src/StreamChatOverlay/Services/KickChatService.cs`
- Test: `tests/StreamChatOverlay.Tests/Services/KickMessageParsingTests.cs`
- Create: `tests/StreamChatOverlay.Tests/TestData/kick-chat-message.json`

**Step 1: Write tests for Kick message parsing**

```csharp
// tests/StreamChatOverlay.Tests/Services/KickMessageParsingTests.cs
using StreamChatOverlay.Models;
using StreamChatOverlay.Services;

namespace StreamChatOverlay.Tests.Services;

public class KickMessageParsingTests
{
    [Fact]
    public void ParseContent_PlainText_ReturnsSingleTextFragment()
    {
        var fragments = KickChatService.ParseContent("hello world");
        Assert.Single(fragments);
        Assert.Equal(FragmentType.Text, fragments[0].Type);
        Assert.Equal("hello world", fragments[0].Content);
    }

    [Fact]
    public void ParseContent_WithEmote_SplitsIntoFragments()
    {
        var fragments = KickChatService.ParseContent("hello [emote:37221:KEKW] world");
        Assert.Equal(3, fragments.Count);
        Assert.Equal("hello ", fragments[0].Content);
        Assert.Equal(FragmentType.Emote, fragments[1].Type);
        Assert.Equal("KEKW", fragments[1].Content);
        Assert.Contains("37221", fragments[1].EmoteUrl!);
        Assert.Equal(" world", fragments[2].Content);
    }

    [Fact]
    public void ParseContent_MultipleEmotes_AllParsed()
    {
        var fragments = KickChatService.ParseContent("[emote:1:PogChamp] nice [emote:2:KEKW]");
        Assert.Equal(3, fragments.Count);
        Assert.Equal(FragmentType.Emote, fragments[0].Type);
        Assert.Equal("PogChamp", fragments[0].Content);
        Assert.Equal(FragmentType.Text, fragments[1].Type);
        Assert.Equal(" nice ", fragments[1].Content);
        Assert.Equal(FragmentType.Emote, fragments[2].Type);
        Assert.Equal("KEKW", fragments[2].Content);
    }

    [Fact]
    public void ParsePusherData_ValidJson_ExtractsMessage()
    {
        var json = """
        {
            "id": "test-123",
            "chatroom_id": 799,
            "content": "hello [emote:37221:KEKW]",
            "type": "message",
            "created_at": "2026-02-18T12:34:56.000000Z",
            "sender": {
                "id": 12345,
                "username": "TestUser",
                "slug": "testuser",
                "identity": {
                    "color": "#FF6347",
                    "badges": [
                        { "type": "subscriber", "text": "Subscriber" }
                    ]
                }
            }
        }
        """;

        var msg = KickChatService.ParseChatMessageJson(json);
        Assert.NotNull(msg);
        Assert.Equal("TestUser", msg!.Username);
        Assert.Equal("#FF6347", msg.UsernameColor);
        Assert.Equal(ChatPlatform.Kick, msg.Platform);
        Assert.Equal(2, msg.Fragments.Count);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/StreamChatOverlay.Tests --filter "KickMessageParsingTests" -v n`
Expected: FAIL - KickChatService does not exist.

**Step 3: Implement KickChatService**

```csharp
// src/StreamChatOverlay/Services/KickChatService.cs
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
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/StreamChatOverlay.Tests --filter "KickMessageParsingTests" -v n`
Expected: 4 passed, 0 failed.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add KickChatService with Pusher WebSocket and emote parsing"
```

---

## Task 5: Emote Resolver (Third-Party Emotes)

**Files:**
- Create: `src/StreamChatOverlay/Services/EmoteResolver.cs`

This service fetches BTTV, FFZ, and 7TV emotes for a Twitch channel and builds a lookup dictionary. When a text fragment contains an emote word, it gets replaced with an emote fragment.

**Step 1: Implement EmoteResolver**

```csharp
// src/StreamChatOverlay/Services/EmoteResolver.cs
using System.Collections.Concurrent;
using System.Text.Json;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Services;

public sealed class EmoteResolver
{
    // word -> (url, isAnimated)
    private readonly ConcurrentDictionary<string, (string Url, bool IsAnimated)> _thirdPartyEmotes = new();
    private readonly HttpClient _http = new();

    /// <summary>
    /// Fetches BTTV, FFZ, and 7TV global + channel emotes for a Twitch channel.
    /// Call once per connect. Username is the Twitch channel name.
    /// </summary>
    public async Task LoadTwitchThirdPartyEmotesAsync(string twitchUsername)
    {
        // Resolve Twitch user ID via decapi (no auth needed)
        string? userId = null;
        try
        {
            userId = (await _http.GetStringAsync(
                $"https://decapi.me/twitch/id/{twitchUsername}")).Trim();
        }
        catch { /* If this fails, we skip channel-specific emotes */ }

        var tasks = new List<Task>
        {
            LoadBttvGlobalAsync(),
            LoadFfzGlobalAsync(),
            Load7TvGlobalAsync()
        };

        if (userId != null)
        {
            tasks.Add(LoadBttvChannelAsync(userId));
            tasks.Add(LoadFfzChannelAsync(twitchUsername));
            tasks.Add(Load7TvChannelAsync(userId));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Takes the text fragments from a ChatMessage and resolves any third-party
    /// emote words into emote fragments.
    /// </summary>
    public List<MessageFragment> ResolveThirdPartyEmotes(List<MessageFragment> fragments)
    {
        if (_thirdPartyEmotes.IsEmpty) return fragments;

        var result = new List<MessageFragment>();
        foreach (var fragment in fragments)
        {
            if (fragment.Type != FragmentType.Text)
            {
                result.Add(fragment);
                continue;
            }

            // Split text by spaces, check each word
            var words = fragment.Content.Split(' ');
            var textBuffer = new List<string>();

            foreach (var word in words)
            {
                if (_thirdPartyEmotes.TryGetValue(word, out var emote))
                {
                    // Flush text buffer
                    if (textBuffer.Count > 0)
                    {
                        result.Add(MessageFragment.Text(string.Join(' ', textBuffer) + " "));
                        textBuffer.Clear();
                    }
                    result.Add(MessageFragment.Emote(word, emote.Url, emote.IsAnimated));
                }
                else
                {
                    textBuffer.Add(word);
                }
            }

            if (textBuffer.Count > 0)
                result.Add(MessageFragment.Text(string.Join(' ', textBuffer)));
        }

        return result;
    }

    private async Task LoadBttvGlobalAsync()
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.betterttv.net/3/cached/emotes/global");
            using var doc = JsonDocument.Parse(json);
            foreach (var emote in doc.RootElement.EnumerateArray())
            {
                var id = emote.GetProperty("id").GetString()!;
                var code = emote.GetProperty("code").GetString()!;
                var type = emote.GetProperty("imageType").GetString();
                _thirdPartyEmotes[code] = ($"https://cdn.betterttv.net/emote/{id}/3x", type == "gif");
            }
        }
        catch { }
    }

    private async Task LoadBttvChannelAsync(string twitchUserId)
    {
        try
        {
            var json = await _http.GetStringAsync(
                $"https://api.betterttv.net/3/cached/users/twitch/{twitchUserId}");
            using var doc = JsonDocument.Parse(json);

            void AddEmotes(JsonElement array)
            {
                foreach (var emote in array.EnumerateArray())
                {
                    var id = emote.GetProperty("id").GetString()!;
                    var code = emote.GetProperty("code").GetString()!;
                    var type = emote.GetProperty("imageType").GetString();
                    _thirdPartyEmotes[code] = ($"https://cdn.betterttv.net/emote/{id}/3x", type == "gif");
                }
            }

            if (doc.RootElement.TryGetProperty("channelEmotes", out var channel))
                AddEmotes(channel);
            if (doc.RootElement.TryGetProperty("sharedEmotes", out var shared))
                AddEmotes(shared);
        }
        catch { }
    }

    private async Task LoadFfzGlobalAsync()
    {
        try
        {
            var json = await _http.GetStringAsync("https://api.frankerfacez.com/v1/set/global");
            using var doc = JsonDocument.Parse(json);
            foreach (var set in doc.RootElement.GetProperty("sets").EnumerateObject())
            {
                foreach (var emote in set.Value.GetProperty("emoticons").EnumerateArray())
                {
                    var id = emote.GetProperty("id").GetInt32();
                    var name = emote.GetProperty("name").GetString()!;
                    _thirdPartyEmotes[name] = ($"https://cdn.frankerfacez.com/emote/{id}/4", false);
                }
            }
        }
        catch { }
    }

    private async Task LoadFfzChannelAsync(string channelName)
    {
        try
        {
            var json = await _http.GetStringAsync(
                $"https://api.frankerfacez.com/v1/room/{channelName}");
            using var doc = JsonDocument.Parse(json);
            foreach (var set in doc.RootElement.GetProperty("sets").EnumerateObject())
            {
                foreach (var emote in set.Value.GetProperty("emoticons").EnumerateArray())
                {
                    var id = emote.GetProperty("id").GetInt32();
                    var name = emote.GetProperty("name").GetString()!;
                    _thirdPartyEmotes[name] = ($"https://cdn.frankerfacez.com/emote/{id}/4", false);
                }
            }
        }
        catch { }
    }

    private async Task Load7TvGlobalAsync()
    {
        try
        {
            var json = await _http.GetStringAsync("https://7tv.io/v3/emote-sets/global");
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("emotes", out var emotes))
            {
                foreach (var emote in emotes.EnumerateArray())
                {
                    var id = emote.GetProperty("id").GetString()!;
                    var name = emote.GetProperty("name").GetString()!;
                    var animated = emote.TryGetProperty("data", out var data)
                        && data.TryGetProperty("animated", out var anim)
                        && anim.GetBoolean();
                    // Use GIF for animated (WPF doesn't support animated WebP)
                    var ext = animated ? "gif" : "webp";
                    _thirdPartyEmotes[name] = ($"https://cdn.7tv.app/emote/{id}/3x.{ext}", animated);
                }
            }
        }
        catch { }
    }

    private async Task Load7TvChannelAsync(string twitchUserId)
    {
        try
        {
            var json = await _http.GetStringAsync($"https://7tv.io/v3/users/twitch/{twitchUserId}");
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("emote_set", out var set)
                && set.TryGetProperty("emotes", out var emotes))
            {
                foreach (var emote in emotes.EnumerateArray())
                {
                    var id = emote.GetProperty("id").GetString()!;
                    var name = emote.GetProperty("name").GetString()!;
                    var animated = emote.TryGetProperty("data", out var data)
                        && data.TryGetProperty("animated", out var anim)
                        && anim.GetBoolean();
                    var ext = animated ? "gif" : "webp";
                    _thirdPartyEmotes[name] = ($"https://cdn.7tv.app/emote/{id}/3x.{ext}", animated);
                }
            }
        }
        catch { }
    }
}
```

**Step 2: Build to verify compilation**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add EmoteResolver for BTTV, FFZ, and 7TV emotes"
```

---

## Task 6: OverlayViewModel

**Files:**
- Create: `src/StreamChatOverlay/ViewModels/OverlayViewModel.cs`

**Step 1: Implement OverlayViewModel**

```csharp
// src/StreamChatOverlay/ViewModels/OverlayViewModel.cs
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamChatOverlay.Models;
using StreamChatOverlay.Services;

namespace StreamChatOverlay.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    private readonly TwitchChatService _twitchService = new();
    private readonly KickChatService _kickService = new();
    private readonly EmoteResolver _emoteResolver = new();

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBorderVisible = true;
    [ObservableProperty] private string _statusText = "Disconnected";
    [ObservableProperty] private AppSettings _settings;

    public OverlayViewModel()
    {
        _settings = AppSettings.Load();

        _twitchService.OnMessageReceived += HandleMessage;
        _twitchService.OnError += err => StatusText = $"Twitch error: {err}";
        _twitchService.OnConnected += () => StatusText = "Twitch connected";
        _twitchService.OnDisconnected += () => StatusText = "Twitch disconnected";

        _kickService.OnMessageReceived += HandleMessage;
        _kickService.OnError += err => StatusText = $"Kick error: {err}";
        _kickService.OnConnected += () => StatusText = "Kick connected";
        _kickService.OnDisconnected += () => StatusText = "Kick disconnected";
    }

    private void HandleMessage(ChatMessage msg)
    {
        // Resolve third-party emotes for Twitch messages
        if (msg.Platform == ChatPlatform.Twitch && Settings.ShowEmotes)
        {
            msg = msg with
            {
                Fragments = _emoteResolver.ResolveThirdPartyEmotes(msg.Fragments)
            };
        }

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Messages.Add(msg);
            while (Messages.Count > Settings.MaxMessages)
                Messages.RemoveAt(0);
        });
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var tasks = new List<Task>();

        if (!string.IsNullOrWhiteSpace(Settings.TwitchUsername))
        {
            StatusText = "Connecting to Twitch...";
            await _emoteResolver.LoadTwitchThirdPartyEmotesAsync(Settings.TwitchUsername);
            tasks.Add(_twitchService.ConnectAsync(Settings.TwitchUsername));
        }

        if (!string.IsNullOrWhiteSpace(Settings.KickUsername))
        {
            StatusText = "Connecting to Kick...";
            tasks.Add(_kickService.ConnectAsync(Settings.KickUsername));
        }

        if (tasks.Count == 0)
        {
            StatusText = "Enter at least one username";
            return;
        }

        try
        {
            await Task.WhenAll(tasks);
            IsConnected = true;
            StatusText = "Connected";
        }
        catch (Exception ex)
        {
            StatusText = $"Connection error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _twitchService.DisconnectAsync();
        await _kickService.DisconnectAsync();
        IsConnected = false;
        StatusText = "Disconnected";
    }

    [RelayCommand]
    private void ToggleBorders()
    {
        IsBorderVisible = !IsBorderVisible;
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
    }

    public void SaveSettings()
    {
        Settings.Save();
    }
}
```

Note: `ChatMessage` must be changed to a `record` to use `with` syntax. Update the `ChatMessage` model:

```csharp
// Update in src/StreamChatOverlay/Models/ChatMessage.cs
public sealed record ChatMessage
{
    // ... same properties but as record
}
```

**Step 2: Build to verify compilation**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add OverlayViewModel with connect/disconnect and message handling"
```

---

## Task 7: Overlay Window (XAML + Code-Behind)

**Files:**
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml` (create)
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml.cs` (create)
- Create: `src/StreamChatOverlay/Behaviors/InlineMessageBehavior.cs`

**Step 1: Create the InlineMessageBehavior (renders emotes inline in TextBlock)**

```csharp
// src/StreamChatOverlay/Behaviors/InlineMessageBehavior.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamChatOverlay.Models;
using WpfAnimatedGif;

namespace StreamChatOverlay.Behaviors;

public static class InlineMessageBehavior
{
    public static readonly DependencyProperty FragmentsProperty =
        DependencyProperty.RegisterAttached(
            "Fragments",
            typeof(IList<MessageFragment>),
            typeof(InlineMessageBehavior),
            new PropertyMetadata(null, OnFragmentsChanged));

    public static void SetFragments(DependencyObject element, IList<MessageFragment>? value)
        => element.SetValue(FragmentsProperty, value);

    public static IList<MessageFragment>? GetFragments(DependencyObject element)
        => (IList<MessageFragment>?)element.GetValue(FragmentsProperty);

    private static void OnFragmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;
        textBlock.Inlines.Clear();

        if (e.NewValue is not IList<MessageFragment> fragments) return;

        foreach (var fragment in fragments)
        {
            if (fragment.Type == FragmentType.Text)
            {
                textBlock.Inlines.Add(new Run(fragment.Content));
            }
            else if (fragment.Type == FragmentType.Emote && fragment.EmoteUrl != null)
            {
                var image = new Image
                {
                    Height = 28,
                    Width = 28,
                    Stretch = Stretch.Uniform,
                    ToolTip = fragment.Content,
                    Margin = new Thickness(2, 0, 2, 0)
                };

                if (fragment.IsAnimated)
                {
                    var uri = new Uri(fragment.EmoteUrl);
                    ImageBehavior.SetAnimatedSource(image, new BitmapImage(uri));
                    ImageBehavior.SetRepeatBehavior(image,
                        System.Windows.Media.Animation.RepeatBehavior.Forever);
                }
                else
                {
                    image.Source = new BitmapImage(new Uri(fragment.EmoteUrl))
                    {
                        CacheOption = BitmapCacheOption.OnLoad
                    };
                }

                textBlock.Inlines.Add(new InlineUIContainer(image)
                {
                    BaselineAlignment = BaselineAlignment.Center
                });
            }
        }
    }
}
```

**Step 2: Create OverlayWindow XAML**

```xml
<!-- src/StreamChatOverlay/Views/OverlayWindow.xaml -->
<Window x:Class="StreamChatOverlay.Views.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:b="clr-namespace:StreamChatOverlay.Behaviors"
        xmlns:vm="clr-namespace:StreamChatOverlay.ViewModels"
        Title="Stream Chat Overlay"
        AllowsTransparency="True"
        WindowStyle="None"
        Topmost="True"
        ShowInTaskbar="True"
        Background="Transparent"
        ResizeMode="NoResize"
        Width="{Binding Settings.WindowWidth, Mode=TwoWay}"
        Height="{Binding Settings.WindowHeight, Mode=TwoWay}"
        Left="{Binding Settings.WindowLeft, Mode=TwoWay}"
        Top="{Binding Settings.WindowTop, Mode=TwoWay}">

    <Window.DataContext>
        <vm:OverlayViewModel/>
    </Window.DataContext>

    <Grid>
        <!-- Main container with semi-transparent background -->
        <Border CornerRadius="8"
                Background="{Binding Settings.BackgroundColor}"
                Opacity="{Binding Settings.Opacity}">
        </Border>

        <DockPanel>
            <!-- Title bar (visible only in setup mode) -->
            <Border DockPanel.Dock="Top"
                    Background="#40FFFFFF"
                    Height="30"
                    CornerRadius="8,8,0,0"
                    MouseLeftButtonDown="TitleBar_MouseLeftButtonDown"
                    Visibility="{Binding IsBorderVisible, Converter={StaticResource BoolToVisibility}}">
                <DockPanel Margin="8,0">
                    <TextBlock Text="Stream Chat Overlay"
                               Foreground="White"
                               VerticalAlignment="Center"
                               FontSize="12"/>
                    <StackPanel DockPanel.Dock="Right"
                                Orientation="Horizontal"
                                HorizontalAlignment="Right">
                        <Button Content="&#x2699;" ToolTip="Settings"
                                Click="Settings_Click"
                                Style="{StaticResource TitleBarButton}"/>
                        <Button Content="&#x2796;" ToolTip="Hide Borders"
                                Command="{Binding ToggleBordersCommand}"
                                Style="{StaticResource TitleBarButton}"/>
                        <Button Content="&#x2716;" ToolTip="Close"
                                Click="Close_Click"
                                Style="{StaticResource TitleBarButton}"/>
                    </StackPanel>
                </DockPanel>
            </Border>

            <!-- Status bar (visible only in setup mode) -->
            <Border DockPanel.Dock="Bottom"
                    Background="#40FFFFFF"
                    Height="24"
                    CornerRadius="0,0,8,8"
                    Visibility="{Binding IsBorderVisible, Converter={StaticResource BoolToVisibility}}">
                <TextBlock Text="{Binding StatusText}"
                           Foreground="#AAFFFFFF"
                           FontSize="11"
                           VerticalAlignment="Center"
                           Margin="8,0"/>
            </Border>

            <!-- Chat messages -->
            <ItemsControl ItemsSource="{Binding Messages}"
                          x:Name="ChatList"
                          Margin="4">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <VirtualizingStackPanel/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <WrapPanel Margin="2">
                            <!-- Platform icon -->
                            <TextBlock Text="{Binding Platform, Converter={StaticResource PlatformToIcon}}"
                                       Foreground="#888888"
                                       FontSize="{Binding DataContext.Settings.FontSize, RelativeSource={RelativeSource AncestorType=Window}}"
                                       Margin="0,0,4,0"
                                       Visibility="{Binding DataContext.Settings.ShowPlatformIcon, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BoolToVisibility}}"/>
                            <!-- Username -->
                            <TextBlock FontWeight="Bold"
                                       FontSize="{Binding DataContext.Settings.FontSize, RelativeSource={RelativeSource AncestorType=Window}}"
                                       Margin="0,0,4,0">
                                <Run Text="{Binding Username, Mode=OneWay}"
                                     Foreground="{Binding UsernameColor, Converter={StaticResource StringToBrush}}"/>
                                <Run Text=": " Foreground="{Binding DataContext.Settings.TextColor, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource StringToBrush}}"/>
                            </TextBlock>
                            <!-- Message with inline emotes -->
                            <TextBlock b:InlineMessageBehavior.Fragments="{Binding Fragments}"
                                       Foreground="{Binding DataContext.Settings.TextColor, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource StringToBrush}}"
                                       FontSize="{Binding DataContext.Settings.FontSize, RelativeSource={RelativeSource AncestorType=Window}}"
                                       TextWrapping="Wrap"/>
                        </WrapPanel>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </DockPanel>

        <!-- Resize grips (visible only in setup mode) -->
        <Thumb x:Name="ResizeGrip" HorizontalAlignment="Right" VerticalAlignment="Bottom"
               Width="16" Height="16" Cursor="SizeNWSE"
               DragDelta="ResizeGrip_DragDelta"
               Visibility="{Binding IsBorderVisible, Converter={StaticResource BoolToVisibility}}">
            <Thumb.Template>
                <ControlTemplate>
                    <Grid Background="Transparent">
                        <Path Data="M 0,16 L 16,0 M 5,16 L 16,5 M 10,16 L 16,10"
                              Stroke="#60FFFFFF" StrokeThickness="1"/>
                    </Grid>
                </ControlTemplate>
            </Thumb.Template>
        </Thumb>
    </Grid>

    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisibility"/>
        <!-- StringToBrush and PlatformToIcon converters defined in code -->

        <Style x:Key="TitleBarButton" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="FontSize" Value="14"/>
            <Setter Property="Width" Value="30"/>
            <Setter Property="Height" Value="30"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="4">
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#40FFFFFF"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>
</Window>
```

**Step 3: Create OverlayWindow code-behind**

```csharp
// src/StreamChatOverlay/Views/OverlayWindow.xaml.cs
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using StreamChatOverlay.ViewModels;

namespace StreamChatOverlay.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newWidth = Width + e.HorizontalChange;
        var newHeight = Height + e.VerticalChange;
        if (newWidth >= 200) Width = newWidth;
        if (newHeight >= 150) Height = newHeight;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = DataContext,
            Owner = this
        };
        settingsWindow.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var vm = (OverlayViewModel)DataContext;
        vm.SaveSettings();
        Application.Current.Shutdown();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        // Window position is two-way bound to settings
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        // Window size is two-way bound to settings
    }
}
```

**Step 4: Create value converters**

```csharp
// src/StreamChatOverlay/Converters/StringToBrushConverter.cs
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StreamChatOverlay.Converters;

public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                return new BrushConverter().ConvertFromString(hex) as Brush ?? Brushes.White;
            }
            catch
            {
                return Brushes.White;
            }
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

```csharp
// src/StreamChatOverlay/Converters/PlatformToIconConverter.cs
using System.Globalization;
using System.Windows.Data;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Converters;

public class PlatformToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ChatPlatform platform
            ? platform == ChatPlatform.Twitch ? "T" : "K"
            : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

Note: Update the XAML `Window.Resources` to include the converters:

```xml
<local:StringToBrushConverter x:Key="StringToBrush"
    xmlns:local="clr-namespace:StreamChatOverlay.Converters"/>
<local:PlatformToIconConverter x:Key="PlatformToIcon"
    xmlns:local="clr-namespace:StreamChatOverlay.Converters"/>
```

**Step 5: Build to verify compilation**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add overlay window with transparent borderless UI and inline emote rendering"
```

---

## Task 8: Settings Window

**Files:**
- Create: `src/StreamChatOverlay/Views/SettingsWindow.xaml`
- Create: `src/StreamChatOverlay/Views/SettingsWindow.xaml.cs`

**Step 1: Create SettingsWindow XAML**

```xml
<!-- src/StreamChatOverlay/Views/SettingsWindow.xaml -->
<Window x:Class="StreamChatOverlay.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Settings"
        Width="400" Height="520"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        Background="#1E1E2E"
        Foreground="White">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="20">
        <StackPanel>
            <TextBlock Text="Stream Chat Overlay Settings" FontSize="18"
                       FontWeight="Bold" Margin="0,0,0,16"/>

            <!-- Connection -->
            <TextBlock Text="CHANNELS" FontSize="11" Foreground="#888"
                       Margin="0,0,0,4"/>
            <TextBlock Text="Twitch Username" Margin="0,0,0,4"/>
            <TextBox Text="{Binding Settings.TwitchUsername, UpdateSourceTrigger=PropertyChanged}"
                     Padding="6" Margin="0,0,0,8"
                     Background="#2A2A3E" Foreground="White" BorderBrush="#444"/>

            <TextBlock Text="Kick Username" Margin="0,0,0,4"/>
            <TextBox Text="{Binding Settings.KickUsername, UpdateSourceTrigger=PropertyChanged}"
                     Padding="6" Margin="0,0,0,12"
                     Background="#2A2A3E" Foreground="White" BorderBrush="#444"/>

            <StackPanel Orientation="Horizontal" Margin="0,0,0,16">
                <Button Content="Connect" Command="{Binding ConnectCommand}"
                        Padding="16,6" Background="#9147FF" Foreground="White"
                        BorderThickness="0" Cursor="Hand" Margin="0,0,8,0"/>
                <Button Content="Disconnect" Command="{Binding DisconnectCommand}"
                        Padding="16,6" Background="#444" Foreground="White"
                        BorderThickness="0" Cursor="Hand"/>
            </StackPanel>

            <Separator Background="#333" Margin="0,0,0,12"/>

            <!-- Appearance -->
            <TextBlock Text="APPEARANCE" FontSize="11" Foreground="#888"
                       Margin="0,0,0,8"/>

            <TextBlock Text="{Binding Settings.FontSize, StringFormat='Font Size: {0:F0}'}"
                       Margin="0,0,0,4"/>
            <Slider Value="{Binding Settings.FontSize}" Minimum="10" Maximum="32"
                    TickFrequency="1" IsSnapToTickEnabled="True" Margin="0,0,0,12"/>

            <TextBlock Text="{Binding Settings.Opacity, StringFormat='Opacity: {0:P0}'}"
                       Margin="0,0,0,4"/>
            <Slider Value="{Binding Settings.Opacity}" Minimum="0.1" Maximum="1.0"
                    TickFrequency="0.05" IsSnapToTickEnabled="True" Margin="0,0,0,12"/>

            <TextBlock Text="{Binding Settings.MaxMessages, StringFormat='Max Messages: {0}'}"
                       Margin="0,0,0,4"/>
            <Slider Value="{Binding Settings.MaxMessages}" Minimum="50" Maximum="500"
                    TickFrequency="50" IsSnapToTickEnabled="True" Margin="0,0,0,12"/>

            <Separator Background="#333" Margin="0,0,0,12"/>

            <!-- Toggles -->
            <TextBlock Text="DISPLAY" FontSize="11" Foreground="#888"
                       Margin="0,0,0,8"/>
            <CheckBox Content="Show Platform Icon (T/K)"
                      IsChecked="{Binding Settings.ShowPlatformIcon}"
                      Foreground="White" Margin="0,0,0,6"/>
            <CheckBox Content="Show Badges"
                      IsChecked="{Binding Settings.ShowBadges}"
                      Foreground="White" Margin="0,0,0,6"/>
            <CheckBox Content="Show Emotes"
                      IsChecked="{Binding Settings.ShowEmotes}"
                      Foreground="White" Margin="0,0,0,12"/>

            <Separator Background="#333" Margin="0,0,0,12"/>

            <Button Content="Save &amp; Close" Click="SaveClose_Click"
                    Padding="16,8" Background="#9147FF" Foreground="White"
                    BorderThickness="0" Cursor="Hand" HorizontalAlignment="Right"/>
        </StackPanel>
    </ScrollViewer>
</Window>
```

**Step 2: Create SettingsWindow code-behind**

```csharp
// src/StreamChatOverlay/Views/SettingsWindow.xaml.cs
using System.Windows;
using StreamChatOverlay.ViewModels;

namespace StreamChatOverlay.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        var vm = (OverlayViewModel)DataContext;
        vm.SaveSettings();
        Close();
    }
}
```

**Step 3: Build**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add settings window with connection and appearance controls"
```

---

## Task 9: System Tray Icon

**Files:**
- Modify: `src/StreamChatOverlay/App.xaml`
- Modify: `src/StreamChatOverlay/App.xaml.cs`

**Step 1: Update App.xaml with TaskbarIcon and startup**

```xml
<!-- src/StreamChatOverlay/App.xaml -->
<Application x:Class="StreamChatOverlay.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:tb="http://www.hardcodet.net/taskbar"
             StartupUri="Views/OverlayWindow.xaml"
             ShutdownMode="OnMainWindowClose">
    <Application.Resources>
        <tb:TaskbarIcon x:Key="TrayIcon"
                        ToolTipText="Stream Chat Overlay"
                        IconSource="/Resources/app.ico">
            <tb:TaskbarIcon.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="Show Settings" Click="TrayShowSettings_Click"/>
                    <MenuItem Header="Toggle Borders" Click="TrayToggleBorders_Click"/>
                    <MenuItem Header="Reset Window Position" Click="TrayResetPosition_Click"/>
                    <Separator/>
                    <MenuItem Header="Clear Chat" Click="TrayClearChat_Click"/>
                    <Separator/>
                    <MenuItem Header="Exit" Click="TrayExit_Click"/>
                </ContextMenu>
            </tb:TaskbarIcon.ContextMenu>
        </tb:TaskbarIcon>
    </Application.Resources>
</Application>
```

**Step 2: Update App.xaml.cs**

```csharp
// src/StreamChatOverlay/App.xaml.cs
using System.Windows;
using H.NotifyIcon;
using StreamChatOverlay.ViewModels;
using StreamChatOverlay.Views;

namespace StreamChatOverlay;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private OverlayViewModel? GetViewModel()
        => (MainWindow as OverlayWindow)?.DataContext as OverlayViewModel;

    private void TrayShowSettings_Click(object sender, RoutedEventArgs e)
    {
        if (MainWindow is OverlayWindow overlay)
        {
            var settings = new SettingsWindow
            {
                DataContext = overlay.DataContext,
                Owner = overlay
            };
            settings.ShowDialog();
        }
    }

    private void TrayToggleBorders_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel()?.ToggleBordersCommand.Execute(null);
    }

    private void TrayResetPosition_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm == null) return;
        vm.Settings.WindowLeft = 100;
        vm.Settings.WindowTop = 100;
        vm.Settings.WindowWidth = 350;
        vm.Settings.WindowHeight = 600;
    }

    private void TrayClearChat_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel()?.ClearChatCommand.Execute(null);
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel()?.SaveSettings();
        Shutdown();
    }
}
```

**Step 3: Create a placeholder app icon**

The app needs an `.ico` file. Generate one or use a placeholder:

```bash
# Use any .ico generator or create a simple one
# For now, we can use a placeholder - the app will work without an icon
# but will show a default icon
```

Add to `.csproj`:
```xml
<ItemGroup>
    <Resource Include="Resources\app.ico" />
</ItemGroup>
```

**Step 4: Build**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add system tray icon with context menu for overlay control"
```

---

## Task 10: Auto-Scroll and Chat ScrollViewer

**Files:**
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml`
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml.cs`

**Step 1: Wrap ItemsControl in ScrollViewer with auto-scroll**

Replace the `ItemsControl` section in OverlayWindow.xaml:

```xml
<ScrollViewer x:Name="ChatScrollViewer"
              VerticalScrollBarVisibility="Hidden"
              HorizontalScrollBarVisibility="Disabled"
              CanContentScroll="True">
    <ItemsControl ItemsSource="{Binding Messages}" x:Name="ChatList" Margin="4">
        <!-- ... same ItemTemplate as before ... -->
    </ItemsControl>
</ScrollViewer>
```

Add auto-scroll in code-behind:

```csharp
// In OverlayWindow constructor, after InitializeComponent:
var vm = (OverlayViewModel)DataContext;
vm.Messages.CollectionChanged += (_, _) =>
{
    ChatScrollViewer.ScrollToEnd();
};
```

**Step 2: Build and manually test**

Run: `dotnet run --project src/StreamChatOverlay`
Expected: Window appears, transparent, borderless, always on top. Tray icon visible.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add auto-scroll to chat and ScrollViewer"
```

---

## Task 11: Integration Testing and Polish

**Step 1: Run the app end-to-end**

Run: `dotnet run --project src/StreamChatOverlay`

Manual test checklist:
- [ ] Window appears transparent and always on top
- [ ] Title bar visible with settings gear, hide borders, close buttons
- [ ] Can drag window by title bar
- [ ] Can resize via bottom-right grip
- [ ] Settings window opens from gear icon or tray right-click
- [ ] Enter Twitch username, click Connect -> chat messages appear
- [ ] Enter Kick username, click Connect -> chat messages appear
- [ ] Emotes render inline (Twitch native + BTTV/7TV/FFZ)
- [ ] Kick emotes render inline
- [ ] "Hide Borders" removes chrome, just floating chat
- [ ] Tray icon right-click: all menu items work
- [ ] Reset Window Position works
- [ ] Settings persist across restart
- [ ] Window stays on top of borderless fullscreen game

**Step 2: Fix any issues found during testing**

Common issues to watch for:
- Emote BitmapImage loading errors (wrap in try/catch in InlineMessageBehavior)
- Thread marshaling issues (ensure all UI updates go through Dispatcher)
- Kick API 403 (User-Agent header)
- Large messages overflowing (TextWrapping should handle this)

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat: integration polish and fixes"
```

---

## Summary of Task Dependencies

```
Task 1: Project Scaffolding
    └── Task 2: Core Models
        ├── Task 3: Twitch Chat Service
        ├── Task 4: Kick Chat Service
        └── Task 5: Emote Resolver
            └── Task 6: OverlayViewModel
                ├── Task 7: Overlay Window UI
                │   └── Task 10: Auto-Scroll
                ├── Task 8: Settings Window
                └── Task 9: System Tray Icon
                    └── Task 11: Integration Testing
```

Tasks 3, 4, 5 can be done in parallel after Task 2.
Tasks 7, 8, 9 can be done in parallel after Task 6.
