using System.Collections.Concurrent;
using System.Net.Http;
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
