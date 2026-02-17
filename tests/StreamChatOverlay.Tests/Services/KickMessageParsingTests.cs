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
