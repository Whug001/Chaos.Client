using Chaos.Client.Controls.World.Hud.Panel;
using ChatModel = Chaos.Client.ViewModel.Chat;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class ChatReplaceTests
{
    [Test]
    public void TryReplaceMessage_SwapsByLineId_AndRaisesEvent()
    {
        var chat = new ChatModel();
        chat.AddMessage("Haneul: hello", Color.White, "Haneul: hello", [], lineId: 5);
        chat.AddMessage("Bob: hi", Color.White, "Bob: hi", []);

        ChatModel.ChatMessage? replaced = null;
        chat.MessageReplaced += (_, now) => replaced = now;

        chat.TryReplaceMessage(5, "[Kor] Haneul: hi", [], "Haneul: hello")
            .Should()
            .BeTrue();

        replaced!.Value.Text.Should().Be("[Kor] Haneul: hi");
        replaced.Value.HoverText.Should().Be("Haneul: hello");
        replaced.Value.Color.Should().Be(Color.White);
        chat.FindMessage(5)!.Value.Text.Should().Be("[Kor] Haneul: hi");
    }

    [Test]
    public void TryReplaceMessage_UnknownOrZero_ReturnsFalse()
    {
        var chat = new ChatModel();
        chat.AddMessage("x", Color.White);

        chat.TryReplaceMessage(0, "y", [], "x").Should().BeFalse();
        chat.TryReplaceMessage(99, "y", [], "x").Should().BeFalse();
        chat.FindMessage(99).Should().BeNull();
    }

    [Test]
    public void TryReplaceMessage_DuplicateId_ReplacesNewest()
    {
        var chat = new ChatModel();
        chat.AddMessage("old", Color.White, lineId: 7);
        chat.AddMessage("new", Color.White, lineId: 7);

        chat.TryReplaceMessage(7, "changed", [], "orig").Should().BeTrue();

        chat.FindMessage(7)!.Value.Text.Should().Be("changed");
    }

    [Test]
    public void Replace_SwapsWrappedRowsInPlace()
    {
        var rows = new List<ChatLine>
        {
            new("a", Color.White, 1),
            new("b1", Color.White, 2),
            new("b2", Color.White, 2),
            new("c", Color.White, 3)
        };

        var (index, removed) = ChatRows.Replace(
            rows,
            2,
            [
                new ChatLine("x1", Color.White, 2, "orig"),
                new ChatLine("x2", Color.White, 2, "orig"),
                new ChatLine("x3", Color.White, 2, "orig")
            ]);

        index.Should().Be(1);
        removed.Should().Be(2);
        rows.Select(r => r.Text).Should().Equal("a", "x1", "x2", "x3", "c");
        rows[2].HoverText.Should().Be("orig");
    }

    //stub measure: 1 px per character, breaking after the last space that fits (else mid-word)
    private static int StubBreak(string text, int maxWidth)
    {
        if (text.Length <= maxWidth)
            return text.Length;

        var space = text.LastIndexOf(' ', maxWidth);

        return space > 0 ? space + 1 : maxWidth;
    }

    [Test]
    public void WrapTooltip_LongText_WrapsWithinWidthAndLosesNothing()
    {
        var text = string.Join(' ', Enumerable.Repeat("annyeonghaseyo", 7)) + " " + new string('x', 30);

        var lines = ChatRows.WrapTooltip(text, 20, StubBreak);

        lines.Count.Should().BeGreaterThan(1);
        lines.Should().OnlyContain(l => l.Length <= 20);
        string.Concat(lines).Should().Be(text.Replace(" ", ""));
    }

    [Test]
    public void WrapTooltip_ShortAndEmpty()
    {
        ChatRows.WrapTooltip("hi there", 20, StubBreak).Should().Equal("hi there");
        ChatRows.WrapTooltip("", 20, StubBreak).Should().BeEmpty();
    }

    [Test]
    public void TooltipY_AboveThenBelowThenTop()
    {
        ChatRows.TooltipY(50, 12, 20, 100).Should().Be(29);
        ChatRows.TooltipY(10, 12, 20, 100).Should().Be(23);
        ChatRows.TooltipY(10, 12, 200, 100).Should().Be(0);
    }

    [Test]
    public void Replace_UnknownOrZeroId_ChangesNothing()
    {
        var rows = new List<ChatLine> { new("a", Color.White, 1) };

        ChatRows.Replace(rows, 9, [new ChatLine("z", Color.White, 9)]).Should().Be((-1, 0));
        ChatRows.Replace(rows, 0, [new ChatLine("z", Color.White)]).Should().Be((-1, 0));
        rows.Should().HaveCount(1);
    }
}
