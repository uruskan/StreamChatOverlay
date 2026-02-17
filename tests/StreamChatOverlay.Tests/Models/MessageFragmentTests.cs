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
