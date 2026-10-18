#region
using Chaos.Client.ViewModel;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchChatTests
{
    [Test]
    [Arguments("[Bo]: good luck", TcgChatKind.Opponent)]
    [Arguments("[bo]> thanks", TcgChatKind.Opponent)]
    [Arguments("[Cy]: hello", TcgChatKind.Other)]
    [Arguments("[Cy]> hi back", TcgChatKind.Other)]
    public void Keeps_every_whisper_and_marks_the_opponent(string message, TcgChatKind kind)
    {
        var chat = new TcgMatchChat("Bo");
        chat.Accept(message, message).Should().BeTrue();
        chat.Lines.Should().ContainSingle().Which.Should().Be(new TcgChatLine(message, kind));
    }

    [Test]
    public void Refuses_non_whispers()
    {
        var chat = new TcgMatchChat("Bo");
        chat.Accept("Bo: not a whisper", "Bo: not a whisper").Should().BeFalse();
        chat.Lines.Should().BeEmpty();
    }

    [Test]
    public void Notices_and_spectator_lines()
    {
        var chat = new TcgMatchChat("Bo");
        chat.AddNotice("Spectators can read this chat.");
        chat.AddSpectatorLine("Bo", "gl");
        chat.AddSpectatorLine("Ana", "hf");
        chat.Lines.Should().Equal(
            new TcgChatLine("Spectators can read this chat.", TcgChatKind.Notice),
            new TcgChatLine("[Bo]: gl", TcgChatKind.Opponent),
            new TcgChatLine("[Ana]: hf", TcgChatKind.Other));
    }

    [Test]
    public void Keeps_the_last_fifty_lines()
    {
        var chat = new TcgMatchChat("Bo");
        for (var i = 0; i < 60; i++) chat.AddNotice($"n{i}");
        chat.Lines.Should().HaveCount(TcgMatchChat.MAX_LINES);
        chat.Lines[0].Text.Should().Be("n10");
    }
}
