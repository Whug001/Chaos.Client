#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     One roster row: the element square, name and level, the health bar with its number and the thinner energy
///     bar, and the current status tags wrapped to two lines. A fallen monster's row is greyed. The row only draws
///     what <see cref="TamerBattleUnitState" /> answers; every string is fitted to the row before it is shown.
/// </summary>
public sealed class TamerBattleRosterRow : UIPanel
{
    public const int HEIGHT = 56;
    public const int WIDTH = 140;

    private const int ENERGY_HEIGHT = 4;
    private const int ENERGY_LEFT = 2;
    private const int ENERGY_TOP = 26;
    private const int ENERGY_WIDTH = 136;
    private const int HEALTH_HEIGHT = 8;
    private const int HEALTH_LEFT = 2;
    private const int HEALTH_TOP = 16;
    private const int HEALTH_WIDTH = 136;

    //the shadowed number is 13 tall and the label pads 1px top and bottom: the glyphs stay at 14, over the bar
    private const int HEALTH_TEXT_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int HEALTH_TEXT_LEFT = 2;
    private const int HEALTH_TEXT_TOP = 13;
    private const int HEALTH_TEXT_WIDTH = 136;
    private const int LEVEL_LEFT = 102;
    private const int LEVEL_TOP = 2;
    private const int LEVEL_WIDTH = 36;
    private const int NAME_LEFT = 14;
    private const int NAME_TOP = 2;
    private const int NAME_WIDTH = 86;
    private const int SQUARE_LEFT = 2;
    private const int SQUARE_SIZE = 8;
    private const int SQUARE_TOP = 4;
    private const int STATUS_LEFT = 2;
    private const int STATUS_TOP = 32;
    private const int STATUS_WIDTH = 136;

    private static readonly Color FallenFill = new(40, 40, 40);
    private static readonly Color RowFill = new(28, 20, 12);

    private readonly UILabel NameLabel;
    private readonly UILabel LevelLabel;
    private readonly UIProgressBar HealthBar;
    private readonly UILabel HealthTextLabel;
    private readonly UIProgressBar EnergyBar;
    private readonly UILabel StatusLabel;
    private Color ElementColour = LegendColors.Gray;
    private bool Alive = true;

    public TamerBattleRosterRow()
    {
        Width = WIDTH;
        Height = HEIGHT;
        IsHitTestVisible = false;

        NameLabel = new UILabel
        {
            X = NAME_LEFT,
            Y = NAME_TOP,
            Width = NAME_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(NameLabel);

        LevelLabel = new UILabel
        {
            X = LEVEL_LEFT,
            Y = LEVEL_TOP,
            Width = LEVEL_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Right,
            ForegroundColor = LegendColors.Silver,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(LevelLabel);

        HealthBar = new UIProgressBar
        {
            X = HEALTH_LEFT,
            Y = HEALTH_TOP,
            Width = HEALTH_WIDTH,
            Height = HEALTH_HEIGHT,
            BackgroundColor = new Color(10, 8, 5),
            BorderColor = LegendColors.DimGray,
            FillColor = TamerBoardView.NearFill,
            IsHitTestVisible = false
        };
        AddChild(HealthBar);

        HealthTextLabel = new UILabel
        {
            X = HEALTH_TEXT_LEFT,
            Y = HEALTH_TEXT_TOP,
            Width = HEALTH_TEXT_WIDTH,
            Height = HEALTH_TEXT_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,

            //a black shadow both sides, as the HUD's health and mana numbers have, over a dark side-coloured fill
            ShadowStyle = ShadowStyle.BothSides,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(HealthTextLabel);

        EnergyBar = new UIProgressBar
        {
            X = ENERGY_LEFT,
            Y = ENERGY_TOP,
            Width = ENERGY_WIDTH,
            Height = ENERGY_HEIGHT,
            BackgroundColor = new Color(10, 8, 5),
            BorderColor = LegendColors.DimGray,
            FillColor = LegendColors.Teal,
            IsHitTestVisible = false
        };
        AddChild(EnergyBar);

        StatusLabel = new UILabel
        {
            X = STATUS_LEFT,
            Y = STATUS_TOP,
            Width = STATUS_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT * 2,
            ForegroundColor = LegendColors.Silver,
            WordWrap = true,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(StatusLabel);
    }

    /// <summary>Stamps the row from one battle unit. The caller hides the row where the side has no unit for it.</summary>
    public void Bind(TamerBattleUnitState unit, bool near)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Alive = unit.Alive;
        ElementColour = unit.Alive ? TamerMonsterRow.ElementColor(unit.Element) : LegendColors.Gray;

        var nameColor = unit.Alive ? LegendColors.White : LegendColors.Gray;
        var dimColor = unit.Alive ? LegendColors.Silver : LegendColors.Gray;

        NameLabel.ForegroundColor = nameColor;
        NameLabel.Text = TamerStableText.FitLines(unit.Name, NAME_WIDTH - 2, 1);
        LevelLabel.ForegroundColor = dimColor;
        LevelLabel.Text = TamerBattleText.LevelText(unit.Level);

        var fraction = unit.MaxHealth > 0 ? (float)unit.Health / unit.MaxHealth : 0f;
        HealthBar.Percent = fraction;
        HealthBar.FillColor = unit.Alive ? TamerBoardView.SideFill(near) : LegendColors.Gray;
        HealthTextLabel.ForegroundColor = nameColor;
        HealthTextLabel.Text = TamerBattleText.HealthText(unit.Health, unit.MaxHealth);

        EnergyBar.Percent = Math.Clamp(unit.Energy, 0, 100) / 100f;
        EnergyBar.FillColor = unit.Alive ? LegendColors.Teal : LegendColors.Gray;

        var tags = string.Join(" ", unit.StatusKeys.Select(TamerBattleText.StatusTag).Where(tag => tag.Length > 0));
        StatusLabel.ForegroundColor = dimColor;
        StatusLabel.Text = TamerStableText.FitLines(tags, STATUS_WIDTH - 2, 2);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        DrawRectClipped(
            spriteBatch,
            new Rectangle(ScreenX, ScreenY, Width, Height),
            Alive ? RowFill : FallenFill);

        DrawBorder(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), LegendColors.DimGray);
        DrawRectClipped(
            spriteBatch,
            new Rectangle(ScreenX + SQUARE_LEFT, ScreenY + SQUARE_TOP, SQUARE_SIZE, SQUARE_SIZE),
            ElementColour);

        foreach (var child in Children)
            if (child.Visible)
                child.Draw(spriteBatch);
    }
}
