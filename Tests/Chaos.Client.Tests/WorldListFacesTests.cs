using Chaos.Client.Controls.World.Popups.WorldList;
using Chaos.Client.Models;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class WorldListFacesTests
{
    private static WorldListEntry Player(
        BaseClass baseClass = BaseClass.Peasant,
        AdvClass advClass = AdvClass.None,
        Continent continent = Continent.None,
        bool master = false,
        bool ability = false,
        bool guilded = false)
        => new("Name", null, baseClass, master, guilded, WorldListColor.White, SocialStatus.Awake, advClass, continent, ability);

    [Test]
    public async Task Buttons_have_the_agreed_faces_and_frames()
    {
        WorldListFaces.Buttons.Select(b => b.Count).Should().Equal(3, 2, 3, 3, 3, 3, 3, 1, 1);

        WorldListFaces.Buttons.SelectMany(b => b).Select(f => f.Label).Should().Equal(
            "Country", "Temuair", "Medenia", "Master", "Ability", "Warrior", "Berserker", "Warlord",
            "Rogue", "Archer", "Assassin", "Wizard", "Arcanist", "Elementalist", "Priest", "Bard", "Plague Doctor",
            "Monk", "Adept", "Druid", "Peasant", "Guild");

        //original faces keep frames 2b / 2b + 1
        for (var b = 0; b < WorldListFaces.BUTTON_COUNT; b++)
        {
            WorldListFaces.Buttons[b][0].NormalFrame.Should().Be(2 * b);
            WorldListFaces.Buttons[b][0].LitFrame.Should().Be(2 * b + 1);
        }

        //new faces are 18 + 2i / 19 + 2i in the agreed order
        var newFaces = WorldListFaces.Buttons.SelectMany(b => b.Skip(1)).ToList();
        newFaces.Select(f => f.Label).Should().Equal(
            "Temuair", "Medenia", "Ability", "Berserker", "Warlord", "Archer", "Assassin", "Arcanist", "Elementalist",
            "Bard", "Plague Doctor", "Adept", "Druid");

        for (var i = 0; i < newFaces.Count; i++)
        {
            newFaces[i].NormalFrame.Should().Be(18 + 2 * i);
            newFaces[i].LitFrame.Should().Be(19 + 2 * i);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Warrior_face_includes_its_medenian_classes_but_Berserker_face_does_not_include_Warlords()
    {
        var berserker = Player(BaseClass.Warrior, AdvClass.Berserker);
        var warlord = Player(BaseClass.Warrior, AdvClass.Warlord);
        var warrior = Player(BaseClass.Warrior);
        var monk = Player(BaseClass.Monk, AdvClass.Druid);

        var faces = WorldListFaces.Buttons[2];

        faces[0].Matches(berserker).Should().BeTrue();
        faces[0].Matches(warlord).Should().BeTrue();
        faces[0].Matches(warrior).Should().BeTrue();
        faces[0].Matches(monk).Should().BeFalse();
        faces[1].Matches(berserker).Should().BeTrue();
        faces[1].Matches(warlord).Should().BeFalse();
        faces[2].Matches(warlord).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Country_Master_Peasant_and_Guild_faces_filter_as_expected()
    {
        var onTemuair = Player(continent: Continent.Temuair);
        var onMedenia = Player(continent: Continent.Medenia, ability: true);
        var elsewhere = Player(master: true, guilded: true);

        WorldListFaces.Buttons[0][0].Matches(elsewhere).Should().BeTrue();
        WorldListFaces.Buttons[0][1].Matches(onTemuair).Should().BeTrue();
        WorldListFaces.Buttons[0][1].Matches(onMedenia).Should().BeFalse();
        WorldListFaces.Buttons[0][2].Matches(onMedenia).Should().BeTrue();
        WorldListFaces.Buttons[0][2].Matches(elsewhere).Should().BeFalse();
        WorldListFaces.Buttons[1][0].Matches(elsewhere).Should().BeTrue();
        WorldListFaces.Buttons[1][1].Matches(onMedenia).Should().BeTrue();
        WorldListFaces.Buttons[1][1].Matches(elsewhere).Should().BeFalse();
        WorldListFaces.Buttons[7][0].Matches(onTemuair).Should().BeTrue();
        WorldListFaces.Buttons[8][0].Matches(elsewhere).Should().BeTrue();
        WorldListFaces.Buttons[8][0].Matches(onTemuair).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Count_counts_players_matching_a_face()
    {
        List<WorldListEntry> players =
        [
            Player(BaseClass.Priest, AdvClass.Bard),
            Player(BaseClass.Priest, AdvClass.PlagueDoctor),
            Player(BaseClass.Priest)
        ];

        WorldListFaces.Count(players, WorldListFaces.Buttons[5][0]).Should().Be(3);
        WorldListFaces.Count(players, WorldListFaces.Buttons[5][1]).Should().Be(1);
        WorldListFaces.Count(players, WorldListFaces.Buttons[5][2]).Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clicking_the_lit_button_turns_it_and_wraps_while_clicking_another_only_selects_it()
    {
        var state = new WorldListFilterState();

        state.ActiveButton.Should().Be(0);
        state.Click(2);                               //select Warrior
        state.ActiveButton.Should().Be(2);
        state.FaceOf(2).Should().Be(0);
        state.Click(2);                               //Berserker
        state.Click(2);                               //Warlord
        state.ActiveFace.Label.Should().Be("Warlord");
        state.Click(2);                               //wraps to Warrior
        state.FaceOf(2).Should().Be(0);
        state.Click(2);                               //Berserker again
        state.Click(4);                               //select Wizard: Warrior button keeps Berserker
        state.ActiveButton.Should().Be(4);
        state.FaceOf(2).Should().Be(1);
        state.FaceOf(4).Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ResetForOpen_lights_Country_on_everyone_and_keeps_other_faces()
    {
        var state = new WorldListFilterState();
        state.Click(0);                               //Country is lit: turns to Temuair
        state.Click(3);                               //select Rogue
        state.Click(3);                               //Archer

        state.ResetForOpen();

        state.ActiveButton.Should().Be(0);
        state.FaceOf(0).Should().Be(0);
        state.ActiveFace.Label.Should().Be("Country");
        state.FaceOf(3).Should().Be(1);

        await Task.CompletedTask;
    }
}
