#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>Page 1: male or female, with what a change costs and what it does to gear.</summary>
public sealed class GenderPage : MirrorPageView
{
    private const int SCALE = 2;
    private const int BUTTON_GAP = 52;
    private const int SELECTOR_TOP = 10;
    private const int NOTE_TOP = 146;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;

    private static readonly string[] NOTE =
    [
        "It also reshapes your Master and Grandmaster",
        "gear, and may change your hairstyle and face",
        "to ones made for the new body.",
        "",
        "Keep your current gender to skip this page."
    ];

    private readonly GenderSelector Selector;
    private readonly UILabel MaleCurrent;
    private readonly UILabel FemaleCurrent;
    private readonly UILabel PriceLine;

    public GenderPage(MirrorActions actions, int width, int height)
        : base(actions, width, height)
    {
        Selector = new GenderSelector(SCALE, BUTTON_GAP);
        Selector.X = (width - Selector.Width) / 2;
        Selector.Y = SELECTOR_TOP;
        Selector.GenderChosen += g => Actions.Select(v => v.SetGender(g));
        AddChild(Selector);

        var buttonSize = GenderSelector.BUTTON_SIZE * SCALE;
        var maleLeft = Selector.X;
        var femaleLeft = Selector.X + buttonSize + BUTTON_GAP;
        var nameTop = SELECTOR_TOP + buttonSize + 6;

        AddLabel(maleLeft, nameTop, buttonSize, alignment: HorizontalAlignment.Center).Text = "Male";
        AddLabel(femaleLeft, nameTop, buttonSize, alignment: HorizontalAlignment.Center).Text = "Female";

        MaleCurrent = AddLabel(maleLeft, nameTop + LINE_HEIGHT, buttonSize, LegendColors.Gray, HorizontalAlignment.Center);
        MaleCurrent.Text = "(current)";
        FemaleCurrent = AddLabel(femaleLeft, nameTop + LINE_HEIGHT, buttonSize, LegendColors.Gray, HorizontalAlignment.Center);
        FemaleCurrent.Text = "(current)";

        PriceLine = AddLabel(0, NOTE_TOP, width, LegendColors.Gray);

        for (var i = 0; i < NOTE.Length; i++)
            AddLabel(0, NOTE_TOP + ((i + 1) * LINE_HEIGHT), width, LegendColors.Gray).Text = NOTE[i];
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;

        Selector.SetSelected(vm.Gender);
        MaleCurrent.Visible = vm.CurrentGender == Gender.Male;
        FemaleCurrent.Visible = vm.CurrentGender == Gender.Female;
        PriceLine.Text = $"Changing gender costs {vm.GenderPrice:N0} gold.";
    }
}
