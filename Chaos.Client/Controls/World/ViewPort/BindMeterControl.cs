#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     The monster tamer's orb bind meter. While an orb is readied it reads "Goblin - Clay 48%" over a bar filled to
///     the catch chance; during a throw the bar is split into three segments that light one per shake, and it ends on
///     "BOUND" or "BROKE FREE".
/// </summary>
/// <remarks>
///     Drawn the way <see cref="OxygenBarControl" /> is -- a one-row bar with its label written inside -- and placed
///     in the same status strip column, one row above the oxygen bar so the two never overlap. Everything it shows
///     comes from <see cref="WorldState.Bind" />; this only draws it.
/// </remarks>
public sealed class BindMeterControl : UIElement
{
    /// <summary>A row of text and a pixel of frame above and below it, the same as the oxygen bar.</summary>
    public const int TOTAL_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;

    /// <summary>
    ///     The least the bar may be squeezed to: "Monstername - Silver 100%" for an ordinary name, plus its frame. A
    ///     longer label widens the bar to fit rather than being cut off.
    /// </summary>
    public const int MINIMUM_WIDTH = (26 * TextRenderer.CHAR_WIDTH) + 2;

    private static readonly Color FrameColor = Color.Black;
    private static readonly Color BackdropColor = new(0, 0, 0, 160);

    //orb amber for the chance and the shakes -- clear of the oxygen blue and the four class-resource colours
    private static readonly Color FillColor = new(224, 164, 56, 230);
    private static readonly Color BoundFillColor = new(88, 196, 96, 230);
    private static readonly Color BrokeFreeFillColor = new(200, 64, 56, 230);
    private static readonly Color TickColor = new(0, 0, 0, 200);
    private static readonly Color TextColor = Color.White;
    private static readonly Color ShadowColor = Color.Black;

    private string DrawnLabel = string.Empty;
    private string? LabelSource;

    public BindMeterControl()
    {
        Width = MINIMUM_WIDTH;
        Height = TOTAL_HEIGHT;
        Visible = false;
        IsHitTestVisible = false;
    }

    /// <summary>Puts the bar in the row the caller has measured for it.</summary>
    public void SetBounds(int x, int y, int width)
    {
        X = x;
        Y = y;
        Width = Math.Max(Math.Max(width, MINIMUM_WIDTH), TextRenderer.MeasureWidth(DrawnLabel) + 4);
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        var bind = WorldState.Bind;

        Visible = bind.Visible;

        //the dat font has no em dash (it falls into the korean glyph range and draws as a blank double-width gap),
        //so the label the model carries is drawn with a plain hyphen
        if (!ReferenceEquals(LabelSource, bind.Label))
        {
            LabelSource = bind.Label;
            DrawnLabel = bind.Label.Replace('—', '-');
        }
    }

    /// <inheritdoc />
    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        var bind = WorldState.Bind;
        var bounds = new Rectangle(ScreenX, ScreenY, Width, TOTAL_HEIGHT);

        DrawRect(spriteBatch, bounds, BackdropColor);

        var innerWidth = Width - 2;
        var innerHeight = TOTAL_HEIGHT - 2;
        var fillWidth = (int)Math.Round(innerWidth * Math.Clamp(bind.Fill, 0f, 1f));

        if (fillWidth > 0)
            DrawRect(
                spriteBatch,
                new Rectangle(ScreenX + 1, ScreenY + 1, Math.Min(fillWidth, innerWidth), innerHeight),
                FillFor(bind));

        //three segments in a throw: ticks at a third and two thirds, over the fill so unlit and lit both read
        if (bind.ShownMode == BindMeterMode.Throw)
            for (var i = 1; i < BindState.SEGMENTS; i++)
            {
                var tickX = ScreenX + 1 + (innerWidth * i / BindState.SEGMENTS);

                DrawRect(spriteBatch, new Rectangle(tickX, ScreenY + 1, 1, innerHeight), TickColor);
            }

        DrawBorder(spriteBatch, bounds, FrameColor);

        var textWidth = TextRenderer.MeasureWidth(DrawnLabel);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(ScreenX + ((Width - textWidth) / 2), ScreenY + 1),
            DrawnLabel,
            TextColor,
            ShadowColor);
    }

    private static Color FillFor(BindState bind)
        => bind.ShownOutcome switch
        {
            BindOutcome.Bound      => BoundFillColor,
            BindOutcome.BrokeFree  => BrokeFreeFillColor,
            _                      => FillColor
        };
}
