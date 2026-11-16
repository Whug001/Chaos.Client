using Chaos.Client.Utilities;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class HsvColorTests
{
    [Test]
    public void TryParseHex_accepts_uppercase_with_hash()
    {
        HsvColor.TryParseHex("#1A2B3C", out var color).Should().BeTrue();
        color.Should().Be(new Color(0x1A, 0x2B, 0x3C));
    }

    [Test]
    public void TryParseHex_accepts_lowercase_without_hash()
    {
        HsvColor.TryParseHex("1a2b3c", out var color).Should().BeTrue();
        color.Should().Be(new Color(0x1A, 0x2B, 0x3C));
    }

    [Test]
    public void TryParseHex_trims_surrounding_whitespace()
    {
        HsvColor.TryParseHex("  #FFF ", out var color).Should().BeTrue();
        color.Should().Be(new Color(0xFF, 0xFF, 0xFF));
    }

    [Test]
    public void TryParseHex_expands_shorthand()
    {
        HsvColor.TryParseHex("#abc", out var color).Should().BeTrue();
        color.Should().Be(new Color(0xAA, 0xBB, 0xCC));
    }

    [Test]
    public void TryParseHex_rejects_empty_hash_and_wrong_lengths()
    {
        HsvColor.TryParseHex("", out _).Should().BeFalse();
        HsvColor.TryParseHex("#", out _).Should().BeFalse();
        HsvColor.TryParseHex("12345", out _).Should().BeFalse();
        HsvColor.TryParseHex("1234567", out _).Should().BeFalse();
    }

    [Test]
    public void TryParseHex_rejects_non_hex_and_inner_spaces()
    {
        HsvColor.TryParseHex("#GG0000", out _).Should().BeFalse();
        HsvColor.TryParseHex("#12 34 56", out _).Should().BeFalse();
    }
}
