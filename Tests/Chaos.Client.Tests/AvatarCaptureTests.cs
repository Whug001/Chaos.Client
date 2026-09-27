using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     The launcher card files must be named after the character that is logged in right now. A client started by a
///     card auto-login can log out and log in to a different character, and that character's look and stats must not
///     overwrite the first card.
/// </summary>
public class AvatarCaptureTests
{
    [Test]
    public void SameCharacterAsTheCard_UsesTheCardName()
        => AvatarCapture.ResolveCardName("Iglis", "Iglis")
                        .Should()
                        .Be("Iglis");

    //the launcher looks for the file by the card's spelling, so a login that differs only in case keeps it
    [Test]
    public void SameCharacterDifferentCase_KeepsTheCardSpelling()
        => AvatarCapture.ResolveCardName("iglis", "Iglis")
                        .Should()
                        .Be("iglis");

    [Test]
    public void DifferentCharacterInTheSameClient_UsesTheLoggedInName()
        => AvatarCapture.ResolveCardName("Iglis", "Tempes")
                        .Should()
                        .Be("Tempes");

    //between logout and the next login there is no character, so nothing may be written
    [Test]
    public void NoCharacterLoggedIn_WritesNothing()
        => AvatarCapture.ResolveCardName("Iglis", "")
                        .Should()
                        .BeNull();
}
