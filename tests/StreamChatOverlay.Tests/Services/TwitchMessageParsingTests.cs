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
