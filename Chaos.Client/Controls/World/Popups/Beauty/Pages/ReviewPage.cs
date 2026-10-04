#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty.Pages;

/// <summary>
///     Page 5: how the player looks now and how they will look, side by side, over a receipt of every change. The
///     shell hides its own preview column on this page; Apply is the shell's footer button.
/// </summary>
public sealed class ReviewPage : MirrorPageView
{
    private const int FIGURE_WIDTH = 260;
    private const int FIGURE_HEIGHT = 160;
    private const int CAPTION_TOP = TextRenderer.CHAR_HEIGHT + 4;
    private const int FIGURE_TOP = CAPTION_TOP + TextRenderer.CHAR_HEIGHT + 2;
    private const int CONTROLS_TOP = FIGURE_TOP + FIGURE_HEIGHT + 6;
    private const int RECEIPT_TOP = CONTROLS_TOP + CustomButton.HEIGHT + 8;
    private const int ROW_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int ROTATE_BUTTON_WIDTH = 28;
    private const int START_OVER_WIDTH = 110;

    //receipt columns, centred on the 560 px page
    private const int NAME_LEFT = 100;
    private const int NAME_WIDTH = 66;
    private const int OLD_LEFT = 170;
    private const int OLD_WIDTH = 110;
    private const int NEW_LEFT = 286;
    private const int NEW_WIDTH = 108;
    private const int PRICE_RIGHT = 460;
    private const int PRICE_WIDTH = 124;

    private static readonly string[] CATEGORIES = ["Gender", "Hairstyle", "Hair color", "Skin", "Face"];

    private readonly MirrorPreview NowFigure;
    private readonly MirrorPreview NewFigure;
    private readonly CustomCheckBox GearToggle;
    private readonly CustomButton StartOver;
    private readonly UILabel[] OldLabels = new UILabel[CATEGORIES.Length];
    private readonly UILabel[] NewLabels = new UILabel[CATEGORIES.Length];
    private readonly UILabel[] PriceLabels = new UILabel[CATEGORIES.Length];
    private readonly UILabel TotalValue;
    private readonly UILabel GoldValue;
    private readonly UILabel StatusLabel;

    private int FacingIndex;
    private bool ShowGear;

    /// <summary>The server's refusal of the last Apply, in red under the receipt. Empty shows nothing.</summary>
    public string Status { get; set; } = string.Empty;

    public ReviewPage(MirrorActions actions, AislingRenderer renderer, Action<int> rotate, Action toggleGear, int width, int height)
        : base(actions, width, height)
    {
        var title = MirrorPages.Title(MirrorPage.Review);
        var pageText = $"page {MirrorPages.COUNT} of {MirrorPages.COUNT}";
        var headingLeft = (width - ((title.Length + 3 + pageText.Length) * TextRenderer.CHAR_WIDTH)) / 2;
        AddLabel(headingLeft, 0, title.Length * TextRenderer.CHAR_WIDTH, LegendColors.Gold).Text = title;
        AddLabel(headingLeft + ((title.Length + 3) * TextRenderer.CHAR_WIDTH), 0, pageText.Length * TextRenderer.CHAR_WIDTH, LegendColors.Gray).Text = pageText;

        var newLeft = width - FIGURE_WIDTH;
        AddLabel(0, CAPTION_TOP, FIGURE_WIDTH, LegendColors.Gray, HorizontalAlignment.Center).Text = "NOW";
        AddLabel(newLeft, CAPTION_TOP, FIGURE_WIDTH, LegendColors.Gold, HorizontalAlignment.Center).Text = "NEW";
        AddLabel(FIGURE_WIDTH, FIGURE_TOP + ((FIGURE_HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), newLeft - FIGURE_WIDTH, LegendColors.Gold, HorizontalAlignment.Center)
            .Text = ">";

        NowFigure = new MirrorPreview(renderer, FIGURE_WIDTH, FIGURE_HEIGHT) { X = 0, Y = FIGURE_TOP, Zoomed = false };
        AddChild(NowFigure);
        NewFigure = new MirrorPreview(renderer, FIGURE_WIDTH, FIGURE_HEIGHT) { X = newLeft, Y = FIGURE_TOP, Zoomed = false };
        AddChild(NewFigure);

        var rotateLeft = new CustomButton("<", ROTATE_BUTTON_WIDTH) { X = 0, Y = CONTROLS_TOP };
        rotateLeft.Clicked += () => rotate(-1);
        AddChild(rotateLeft);

        var rotateRight = new CustomButton(">", ROTATE_BUTTON_WIDTH) { X = ROTATE_BUTTON_WIDTH + 4, Y = CONTROLS_TOP };
        rotateRight.Clicked += () => rotate(+1);
        AddChild(rotateRight);

        GearToggle = MirrorPreview.CreateGearToggle(newLeft, CONTROLS_TOP + ((CustomButton.HEIGHT - CustomCheckBox.CHECKBOX_SIZE) / 2));
        GearToggle.Clicked += () => toggleGear();
        AddChild(GearToggle);

        StartOver = new CustomButton("Start over", START_OVER_WIDTH) { X = width - START_OVER_WIDTH, Y = CONTROLS_TOP };
        StartOver.Clicked += () => Actions.Select(v => v.Reset());
        AddChild(StartOver);

        for (var i = 0; i < CATEGORIES.Length; i++)
        {
            var y = RECEIPT_TOP + (i * ROW_HEIGHT);
            AddLabel(NAME_LEFT, y, NAME_WIDTH).Text = CATEGORIES[i];
            OldLabels[i] = AddLabel(OLD_LEFT, y, OLD_WIDTH, LegendColors.Gray, HorizontalAlignment.Right);
            NewLabels[i] = AddLabel(NEW_LEFT, y, NEW_WIDTH, LegendColors.SpringGreen);
            PriceLabels[i] = AddLabel(PRICE_RIGHT - PRICE_WIDTH, y, PRICE_WIDTH, alignment: HorizontalAlignment.Right);
        }

        var totalTop = RECEIPT_TOP + (CATEGORIES.Length * ROW_HEIGHT) + 6;
        AddLabel(NAME_LEFT, totalTop, NAME_WIDTH, LegendColors.Gold).Text = "TOTAL";
        TotalValue = AddLabel(PRICE_RIGHT - PRICE_WIDTH, totalTop, PRICE_WIDTH, LegendColors.Gold, HorizontalAlignment.Right);
        AddLabel(NAME_LEFT, totalTop + ROW_HEIGHT, NAME_WIDTH, LegendColors.Gray).Text = "You have";
        GoldValue = AddLabel(PRICE_RIGHT - PRICE_WIDTH, totalTop + ROW_HEIGHT, PRICE_WIDTH, LegendColors.Gray, HorizontalAlignment.Right);
        StatusLabel = AddLabel(0, totalTop + (3 * ROW_HEIGHT), width, LegendColors.Red, HorizontalAlignment.Center);
    }

    /// <summary>The shell's facing and Show gear state, which both figures follow. Call before <see cref="Refresh" />.</summary>
    public void SetView(int facingIndex, bool showGear)
    {
        FacingIndex = facingIndex;
        ShowGear = showGear;
        GearToggle.Checked = showGear;
    }

    public override void Refresh()
    {
        var vm = WorldState.BeautyShop;

        NowFigure.Refresh(MirrorLooks.Now(vm, ShowGear), FacingIndex);
        NewFigure.Refresh(MirrorLooks.New(vm, ShowGear), FacingIndex);

        SetRow(0, vm.GenderChanged, vm.CurrentGender.ToString(), vm.Gender.ToString(), vm.GenderPrice);
        SetRow(
            1,
            vm.HairstyleChanged,
            Position(vm.HairstylesFor(vm.CurrentGender), h => h.Sprite == vm.CurrentHairStyle),
            Position(vm.Hairstyles, h => h.Sprite == vm.HairStyle),
            vm.CostOfHairStyle(vm.HairStyle));
        SetRow(2, vm.HairColorChanged, vm.CurrentHairColor.ToString(), vm.HairColor.ToString(), vm.CostOfHairColor(vm.HairColor));
        SetRow(3, vm.BodyColorChanged, vm.CurrentBodyColor.ToString(), vm.BodyColor.ToString(), vm.CostOfBodyColor(vm.BodyColor));
        SetRow(4, vm.FaceChanged, FaceName(vm, vm.CurrentFaceSprite), FaceName(vm, vm.FaceSprite), vm.CostOfFace(vm.FaceSprite));

        TotalValue.Text = vm.Total.ToString("N0");
        TotalValue.ForegroundColor = vm.CanAfford ? LegendColors.Gold : LegendColors.Red;
        GoldValue.Text = vm.Gold.ToString("N0");
        StartOver.Enabled = vm.HasUnsavedChanges;
        StatusLabel.Text = Status;
    }

    /// <summary>Drops both figures' textures; the shell calls this when the window hides.</summary>
    public void Release()
    {
        NowFigure.Release();
        NewFigure.Release();
    }

    private void SetRow(int row, bool changed, string from, string to, int price)
    {
        OldLabels[row].Text = changed ? $"{from} >" : string.Empty;
        NewLabels[row].Text = changed ? to : "no change";
        NewLabels[row].ForegroundColor = changed ? LegendColors.SpringGreen : LegendColors.Gray;
        PriceLabels[row].Text = !changed ? string.Empty
            : price == 0 ? "free"
            : price.ToString("N0");
    }
}
