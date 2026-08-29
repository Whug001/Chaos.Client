#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     A two-segment MALE | FEMALE toggle. The selected segment is gold-filled with dark text; the other is a
///     dim/charcoal fill with light text. Clicking the unselected segment raises <see cref="GenderChosen" /> --
///     the panel decides whether/when to call <see cref="SetSelected" /> in response.
/// </summary>
public sealed class GenderSelector : UIPanel
{
    public const int SEGMENT_WIDTH = 72;
    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + 8;

    private readonly UILabel MaleLabel;
    private readonly UILabel FemaleLabel;
    private Gender SelectedGender = Gender.Male;
    private Gender? HoveredGender;

    public event Action<Gender>? GenderChosen;

    public GenderSelector()
    {
        Background = null;
        Width = SEGMENT_WIDTH * 2;
        Height = HEIGHT;

        MaleLabel = new UILabel
        {
            X = 0,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = SEGMENT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.AlmostBlack,
            IsHitTestVisible = false,
            Text = "MALE"
        };
        AddChild(MaleLabel);

        FemaleLabel = new UILabel
        {
            X = SEGMENT_WIDTH,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = SEGMENT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.LightGray,
            IsHitTestVisible = false,
            Text = "FEMALE"
        };
        AddChild(FemaleLabel);
    }

    /// <summary>Sets which segment reads as selected and flips both labels' colours to match.</summary>
    public void SetSelected(Gender gender)
    {
        SelectedGender = gender;
        MaleLabel.ForegroundColor = gender == Gender.Male ? LegendColors.AlmostBlack : LegendColors.LightGray;
        FemaleLabel.ForegroundColor = gender == Gender.Female ? LegendColors.AlmostBlack : LegendColors.LightGray;
    }

    private static Gender SegmentAt(int localX) => localX < SEGMENT_WIDTH ? Gender.Male : Gender.Female;

    public override void OnMouseMove(MouseMoveEvent e) => HoveredGender = SegmentAt(e.ScreenX - ScreenX);

    public override void OnMouseLeave() => HoveredGender = null;

    public override void OnClick(ClickEvent e)
    {
        var chosen = SegmentAt(e.ScreenX - ScreenX);
        e.Handled = true;

        if (chosen != SelectedGender)
            GenderChosen?.Invoke(chosen);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //fills/border first -- base.Draw (UIPanel) paints the MALE/FEMALE labels on top of them
        DrawSegmentFill(spriteBatch, Gender.Male, 0);
        DrawSegmentFill(spriteBatch, Gender.Female, SEGMENT_WIDTH);
        DrawBorder(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), LegendColors.Gold);

        base.Draw(spriteBatch);
    }

    private void DrawSegmentFill(SpriteBatch spriteBatch, Gender gender, int offsetX)
    {
        var bounds = new Rectangle(ScreenX + offsetX, ScreenY, SEGMENT_WIDTH, Height);
        var selected = gender == SelectedGender;
        var fill = selected ? LegendColors.Gold : HoveredGender == gender ? LegendColors.DimGray : LegendColors.Charcoal;
        DrawRect(spriteBatch, bounds, fill);
    }
}
