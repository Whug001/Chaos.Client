#region
using Chaos.Client.ViewModel;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgChatCommandTests
{
    private static TcgChatSend Parse(string text, bool practice = false, params string[] senders)
        => TcgChatCommand.Parse(text, "Bo", senders, practice);

    [Test] public void Plain_text_goes_to_the_opponent() => Parse("gl").Should().Be(new TcgChatSend("Bo", "gl", null));
    [Test] public void Reply_goes_to_the_last_whisperer() => Parse("/r ok", false, "Cy", "Di").Should().Be(new TcgChatSend("Cy", "ok", null));
    [Test] public void Reply_with_no_whisperer_is_a_notice() => Parse("/r ok").Should().Be(new TcgChatSend(null, "", "No one has whispered you."));
    [Test] public void Whisper_names_the_target() => Parse("/w Di see you soon").Should().Be(new TcgChatSend("Di", "see you soon", null));
    [Test] public void Practice_plain_text_is_a_notice() => Parse("gl", true).Should().Be(new TcgChatSend(null, "", "Your opponent can't read chat."));
    [Test] public void Practice_still_allows_whispers() => Parse("/w Di hi", true).Should().Be(new TcgChatSend("Di", "hi", null));

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("/w Di")]
    [Arguments("/r")]
    public void Nothing_to_send(string text) => Parse(text, false, "Cy").Should().Be(TcgChatSend.Nothing);
}
