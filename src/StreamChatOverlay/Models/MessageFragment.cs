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
