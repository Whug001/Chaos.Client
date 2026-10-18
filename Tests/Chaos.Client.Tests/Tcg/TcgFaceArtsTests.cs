using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TcgFaceArtsTests
{
    private static readonly Func<string, int> AllFiles = Files(("tcgart007.spf", 6), ("tcgframe.spf", 7), ("tcgframex.spf", 7), ("tcgframef.spf", 7));

    private static TcgCatalogCardView Card(ushort painting, TcgRarity rarity = TcgRarity.Rare, TcgElement element = TcgElement.Water)
        => new(
            "imp",
            "Imp",
            element,
            rarity,
            TcgCardType.Creature,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            TcgKeyword.None,
            string.Empty,
            TcgArtKind.Creature,
            206,
            TcgTargetKind.None,
            painting);

    private static Func<string, int> Files(params (string Name, int Frames)[] files)
        => name => files.FirstOrDefault(file => file.Name == name).Frames;

    [Test]
    public void No_painting_draws_the_sprite()
    {
        foreach (var style in Enum.GetValues<TcgCardStyle>())
            TcgFaceArts.Resolve(Card(0), style, AllFiles).Should().Be(TcgFaceArt.Sprite);
    }

    [Test]
    public void A_missing_painting_file_draws_the_sprite()
        => TcgFaceArts.Resolve(Card(8), TcgCardStyle.Full, AllFiles).Should().Be(TcgFaceArt.Sprite);

    [Test]
    public void Each_style_draws_its_painting()
    {
        TcgFaceArts.Resolve(Card(7), TcgCardStyle.Normal, AllFiles).Should().Be(TcgFaceArt.Normal);
        TcgFaceArts.Resolve(Card(7), TcgCardStyle.Extended, AllFiles).Should().Be(TcgFaceArt.Extended);
        TcgFaceArts.Resolve(Card(7), TcgCardStyle.Full, AllFiles).Should().Be(TcgFaceArt.Full);
    }

    [Test]
    public void Full_art_needs_a_rare_or_better_card()
    {
        TcgFaceArts.Resolve(Card(7, TcgRarity.Uncommon), TcgCardStyle.Full, AllFiles).Should().Be(TcgFaceArt.Extended);
        TcgFaceArts.Resolve(Card(7, TcgRarity.Mythic), TcgCardStyle.Full, AllFiles).Should().Be(TcgFaceArt.Full);
    }

    [Test]
    public void Old_frame_files_drop_to_the_normal_painting()
    {
        var old = Files(("tcgart007.spf", 6), ("tcgframe.spf", 6));

        TcgFaceArts.Resolve(Card(7), TcgCardStyle.Extended, old).Should().Be(TcgFaceArt.Normal);
        TcgFaceArts.Resolve(Card(7), TcgCardStyle.Full, old).Should().Be(TcgFaceArt.Normal);
    }

    [Test]
    public void Painting_files_have_three_digits()
    {
        TcgFaceArts.PaintingFile(7).Should().Be("tcgart007.spf");
        TcgFaceArts.PaintingFile(121).Should().Be("tcgart121.spf");
    }

    [Test]
    public void A_mythic_uses_frame_6_when_the_file_has_it()
    {
        TcgFaceArts.FrameIndex(Card(7, TcgRarity.Mythic, TcgElement.Fire), 7).Should().Be(6);
        TcgFaceArts.FrameIndex(Card(7, TcgRarity.Mythic, TcgElement.Fire), 6).Should().Be(0);
        TcgFaceArts.FrameIndex(Card(7, TcgRarity.Rare, TcgElement.Earth), 7).Should().Be(3);
    }
}
