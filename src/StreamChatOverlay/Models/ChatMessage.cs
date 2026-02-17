namespace StreamChatOverlay.Models;

public enum ChatPlatform { Twitch, Kick }

public sealed record ChatMessage
{
    public required string Id { get; init; }
    public required ChatPlatform Platform { get; init; }
    public required string Username { get; init; }
    public required string UsernameColor { get; init; }
    public required List<MessageFragment> Fragments { get; init; }
    public required List<string> BadgeUrls { get; init; }
    public required DateTime Timestamp { get; init; }
}
