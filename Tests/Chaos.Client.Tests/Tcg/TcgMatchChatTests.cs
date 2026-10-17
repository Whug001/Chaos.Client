#region
using Chaos.Client.ViewModel;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchChatTests
{
    [Test]
    [Arguments("[Bo]: good luck", true)]
    [Arguments("[bo]> thanks", true)]
    [Arguments("[Cy]: hello", false)]
    [Arguments("Bo: not a whisper", false)]
    public void Keeps_only_whispers_with_the_opponent(string message, bool kept)
    {
        var chat = new TcgMatchChat("Bo");

        chat.Accept(message, message).Should().Be(kept);
        chat.Lines.Should().HaveCount(kept ? 1 : 0);
    }

    [Test]
    public void Shows_the_display_text_and_caps_lines()
    {
        var chat = new TcgMatchChat("Bo");

        for (var i = 0; i < 60; i++)
            chat.Accept("[Bo]: raw", "[Bo]: shown");

        chat.Lines.Should().HaveCount(TcgMatchChat.MAX_LINES);
        chat.Lines[0].Should().Be("[Bo]: shown");
    }
}
