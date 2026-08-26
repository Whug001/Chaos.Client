#region
using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Wheel;

/// <summary>
///     A single wheel -- rotates at constant speed, then eases onto a stop index the server chose. Mirrors
///     <see cref="Slots.ReelControl" />'s animation state machine exactly (<c>Spinning</c>/<c>Settling</c>/
///     <c>SettleElapsed</c>/<c>SettleFrom</c>/<c>SettleTo</c>, the same <see cref="Update(float)" /> signature, the
///     same cubic ease-out), just over an ANGLE in radians instead of a pixel offset. The wheel never decides its
///     own outcome -- it only animates toward one it was given.
/// </summary>
/// <remarks>
///     <para>
///         The colour bands (the wedge shapes, tinted by <see cref="WheelSegmentInfo.Kind" />) are one texture,
///         generated once per <see cref="SetSegments" /> call via SkiaSharp -- the same technique
///         <c>SlotMachineControl.BuildRecessedPanel</c> uses for its own static art -- stored in the inherited
///         <see cref="UIPanel.Background" /> field so disposal falls out of <see cref="UIPanel.Dispose" /> for
///         free, and rotated as a single sprite in <see cref="Draw" /> every frame.
///     </para>
///     <para>
///         The segment LABELS are drawn separately, every frame, via <c>TextRenderer</c> -- the game's own bitmap
///         font, not a SkiaSharp-rendered one -- so they read in the same typeface as every other control on
///         screen. They are deliberately NOT rotated with the wedges: this font has no rotated glyphs to fall back
///         on, and an upright label read at a glance beats one that is only the right way up once a second. Both
///         draws only ever cost an orbit-position calculation per segment per frame -- the archive/colour
///         resolution happened once, in <see cref="SetSegments" />.
///     </para>
/// </remarks>
public sealed class WheelControl : UIPanel
{
    //default footprint, mirroring how ReelControl fixes its own CELL_W/CELL_H*3 in its constructor rather than
    //taking a size from the caller -- unlike ReelControl, though, the owning panel now has a real reason to want a
    //different footprint (a bigger wheel), so this is threaded through the constructor instead of staying fixed.
    public const int DEFAULT_DIAMETER = 140;

    //instance footprint, set once at construction and read by every geometry calculation below (Draw/RebuildFace)
    //instead of a shared const -- so two WheelControls (the panel's Main/Bonus pair) could in principle render at
    //different sizes, though today they are always constructed with the same value.
    private readonly int Diameter;

    //inset from the control's own edge so the wedge circle's antialiased rim never touches the bounding box --
    //without this a 1px sliver of the circle could be clipped away at the very edge of the control's own bounds.
    private const int RIM_INSET = 3;

    //how far in from the rim each label's centre sits. Sized against the longest label this control ever draws
    //("JACKPOT", 7 chars * TextRenderer's 6px advance = 42px wide, half-width 21) rather than a guess: a label's
    //farthest corner from the wheel's own centre is at most labelRadius + sqrt(21^2 + (CHAR_HEIGHT/2)^2) =
    //labelRadius + ~21.8px (Cauchy-Schwarz bound over every possible segment angle), and that must stay inside
    //the drawn rim (DIAMETER/2 - RIM_INSET) with a couple of px to spare -- 24 is the smallest inset clearing that
    //bound at every DIAMETER this control has ever been asked to render at, including the enlarged 180px wheel.
    private const int LABEL_RADIUS_INSET = 24;

    private const float SPIN_SPEED = 6.0f; //radians per second
    private const float SETTLE_SECONDS = 0.6f;

    //── wedge fill colours, resolved once per SetSegments call, never per frame ──
    //deep violet twin shades for the common Multiplier bands, alternated by index parity -- most of any wheel's
    //segments are Multiplier, and a single flat colour for all of them would blur into one solid ring while
    //spinning, with no way to tell where one wedge ends and the next begins.
    private static readonly SKColor MultiplierBandA = new(58, 44, 92, 255);
    private static readonly SKColor MultiplierBandB = new(42, 32, 70, 255);

    //vivid green for Bonus -- distinct in hue from both the violet multiplier bands and Jackpot's gold, so the
    //blurred ring during a fast spin can still be told apart by colour alone.
    private static readonly SKColor BonusBand = new(38, 138, 88, 255);

    //gold for Jackpot -- the same colour SlotMachineControl.RefreshPaytableRows uses for its own jackpot rows
    //(LegendColors.Gold), so "gold" means "jackpot" consistently across the whole feature.
    private static readonly SKColor JackpotBand = new(214, 170, 44, 255);

    private static readonly SKColor RimColor = new(20, 16, 10, 255);

    //── label text colours, resolved once per SetSegments call alongside the wedge fills above ──
    private static readonly Color MultiplierTextColor = LegendColors.White;
    private static readonly Color BonusTextColor = LegendColors.White;

    //dark text on the bright gold Jackpot wedge -- white would wash out against it.
    private static readonly Color JackpotTextColor = LegendColors.AlmostBlack;

    //owned copy -- SetSegments receives a collection owned by the wheel view model (a tier's MainWheel/BonusWheel
    //list), which may be replaced wholesale on the next Open. Aliasing it here would let a later view-model
    //update silently change what this control is mid-animation over. See ReelControl.SetStrip for the same
    //reasoning in one dimension.
    private WheelSegmentInfo[] Segments = [];

    //one label colour per entry in Segments, resolved once here rather than re-switching on Kind every frame in
    //Draw -- the same reason ReelControl resolves idle poses once in SetStrip instead of in its own Draw.
    private Color[] LabelColors = [];

    private float Angle; //radians, increases without bound while spinning/settling and wraps to [0, TwoPi) at rest
    private bool Spinning;
    private bool Settling;
    private float SettleElapsed;
    private float SettleFrom;
    private float SettleTo;

    /// <summary>
    ///     True once the wheel is neither spinning nor easing into its landing position. False for the entire
    ///     settle ease, not just while spinning -- the panel (Task 15) gates its reveal on this, and a wheel that
    ///     reports settled while still turning would show the result before the wheel visibly agrees with it.
    /// </summary>
    public bool IsSettled => !Spinning && !Settling;

    /// <summary>
    ///     Fires once each time a segment boundary crosses the pointer (fixed at the top of the wheel). The panel
    ///     plays a ratchet tick from this, so the tick rate falls out of the easing curve for free -- the
    ///     deceleration IS the tension, and it costs nothing beyond the latch. Fires from both the constant-
    ///     velocity spin and the settling ease; never fires twice for the same boundary crossing.
    /// </summary>
    public event Action? SegmentPassed;

    public WheelControl(int diameter = DEFAULT_DIAMETER)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(diameter, 0);

        Diameter = diameter;
        Width = Diameter;
        Height = Diameter;
    }

    /// <summary>
    ///     Configures the wheel's segments and resolves every per-segment colour once, here -- not in
    ///     <see cref="Draw" />, where it would cost a Kind switch per segment per frame. Resets any in-flight
    ///     spin/settle state, since a fresh segment set invalidates stale angles computed against the previous one.
    /// </summary>
    public void SetSegments(IReadOnlyList<WheelSegmentInfo> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        Segments = [..segments];
        LabelColors = Segments.Select(s => ResolveLabelColor(s.Kind))
                              .ToArray();

        Angle = 0f;
        Spinning = false;
        Settling = false;

        RebuildFace();
    }

    /// <summary>
    ///     Begins continuous rotation. No-op until <see cref="SetSegments" /> has supplied at least one segment.
    /// </summary>
    public void StartSpin()
    {
        if (Segments.Length == 0)
            return;

        Spinning = true;
        Settling = false;
    }

    /// <summary>
    ///     Eases onto <paramref name="stop" /> over <see cref="SETTLE_SECONDS" /> with a cubic ease-out. Always
    ///     travels forward by at least one full revolution: the shortest angular path to a stop just behind the
    ///     pointer is backwards more often than not, and a wheel that snaps backwards to land reads as broken --
    ///     the physical object it represents cannot do that. No-op until <see cref="SetSegments" /> has supplied at
    ///     least one segment.
    /// </summary>
    /// <remarks>
    ///     Idempotent-ish, mirroring <see cref="Slots.ReelControl.LandOn" />: a second call while already easing is
    ///     a no-op rather than restarting the ease, because the panel's landing latch is expected to call this
    ///     exactly once per stop but can fire more than once in practice (see the caller's own latch).
    /// </remarks>
    public void LandOn(int stop)
    {
        if (Segments.Length == 0)
            return;

        if (Settling)
            return;

        ArgumentOutOfRangeException.ThrowIfNegative(stop);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(stop, Segments.Length);

        //segment `stop` sits under the pointer once Angle reaches -(stop+0.5)*arc (mod TwoPi) -- see SegmentArc's
        //own remarks for why the sign is negative. The while loop below then pushes that target strictly more
        //than one full revolution ahead of the current Angle, guaranteeing forward travel.
        var target = -(stop + 0.5f) * SegmentArc;

        while (target <= (Angle + MathHelper.TwoPi))
            target += MathHelper.TwoPi;

        SettleFrom = Angle;
        SettleTo = target;
        SettleElapsed = 0f;
        Settling = true;
        Spinning = false;
    }

    /// <summary>
    ///     Advances the spin/settle animation. Distinct from the inherited GameTime-based
    ///     <see cref="UIElement.Update(GameTime)" /> -- like <see cref="Slots.ReelControl.Update(float)" />, this
    ///     takes elapsed seconds directly and must be called explicitly by the panel driving it.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (Segments.Length == 0)
            return;

        if (Spinning)
        {
            var previous = Angle;

            //advance on an unwrapped value first so the crossing check below always sees a genuine forward step,
            //then wrap the STORED angle into [0, TwoPi) -- mirrors ReelControl.Update wrapping Offset every frame
            //during a spin, done here for float precision over a long session rather than for correctness.
            var raw = Angle + (SPIN_SPEED * deltaSeconds);
            FireSegmentCrossings(previous, raw);
            Angle = raw % MathHelper.TwoPi;

            return;
        }

        if (!Settling)
            return;

        var previousAngle = Angle;
        SettleElapsed += deltaSeconds;
        var t = Math.Clamp(SettleElapsed / SETTLE_SECONDS, 0f, 1f);
        var eased = 1f - ((1f - t) * (1f - t) * (1f - t)); //cubic ease-out

        Angle = SettleFrom + ((SettleTo - SettleFrom) * eased);
        FireSegmentCrossings(previousAngle, Angle);

        if (t < 1f)
            return;

        Angle %= MathHelper.TwoPi;
        Settling = false;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        //UIPanel.Draw is not called -- this control replaces its background/children handling entirely with its
        //own wedge + label rendering (mirrors ReelControl.Draw), so ClipRect must still be refreshed here.
        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        if (Segments.Length == 0)
            return;

        var center = new Vector2(ScreenX + (Diameter / 2f), ScreenY + (Diameter / 2f));
        var origin = new Vector2(Diameter / 2f, Diameter / 2f);

        //the wedge-coloured face, rotated as one sprite about its own centre. Its visible content is a circle
        //inscribed well within the Diameter x Diameter square (see RebuildFace's `radius`), so rotating it about
        //its centre never carries any visible pixel outside this control's own bounds -- only the fully
        //transparent corners of the square ever swing past that box, and those composite as nothing.
        if (Background is not null)
            spriteBatch.Draw(
                Background,
                center,
                null,
                Color.White,
                Angle,
                origin,
                1f,
                SpriteEffects.None,
                0f);

        var labelRadius = (Diameter / 2f) - RIM_INSET - LABEL_RADIUS_INSET;
        var sweep = SegmentArc;

        for (var i = 0; i < Segments.Length; i++)
        {
            //the segment's original (pre-rotation) mid-angle, advanced by the wheel's current rotation to find
            //where it currently sits on screen. -PiOver2 converts from "clockwise from up" (this control's and
            //RebuildFace's shared convention) to the "clockwise from east" convention MathF.Cos/Sin expect.
            var midLocal = (i + 0.5f) * sweep;
            var screenAngle = midLocal + Angle - MathHelper.PiOver2;

            var labelCenter = new Vector2(
                center.X + (labelRadius * MathF.Cos(screenAngle)),
                center.Y + (labelRadius * MathF.Sin(screenAngle)));

            var label = Segments[i].Label;
            var textWidth = TextRenderer.MeasureWidth(label);

            var position = new Vector2(
                labelCenter.X - (textWidth / 2f),
                labelCenter.Y - (TextRenderer.CHAR_HEIGHT / 2f));

            //clipped against ClipRect, same as every other control's text -- the definitive guarantee that a
            //long server-authored label can never spill past this control's own bounds, regardless of the
            //conservative LABEL_RADIUS_INSET margin above.
            DrawTextClipped(spriteBatch, position, label, LabelColors[i]);
        }
    }

    /// <summary>
    ///     The angular width of one segment. Segment <c>i</c> occupies local angle range
    ///     <c>[i*SegmentArc, (i+1)*SegmentArc)</c>, measured clockwise from "up" in the wheel's own unrotated
    ///     frame -- the same frame <see cref="RebuildFace" /> paints the wedges in.
    /// </summary>
    /// <remarks>
    ///     <see cref="LandOn" /> targets <c>-(stop+0.5)*SegmentArc</c> rather than the positive angle: the pointer
    ///     is fixed on screen while the FACE rotates by <c>+Angle</c> (see <see cref="Draw" />), so the segment
    ///     that ends up under the pointer at a given <see cref="Angle" /> is the one whose local angle equals
    ///     <c>-Angle</c> (mod <see cref="MathHelper.TwoPi" />), not <c>+Angle</c>.
    /// </remarks>
    private float SegmentArc => MathHelper.TwoPi / Segments.Length;

    /// <summary>
    ///     Fires <see cref="SegmentPassed" /> once for every multiple of <see cref="SegmentArc" /> that
    ///     <paramref name="currentAngle" /> has passed since <paramref name="previousAngle" />. Both callers pass a
    ///     monotonically non-decreasing pair for the frame just elapsed (the spin branch computes its next angle
    ///     before wrapping it; the settle branch's eased angle only ever increases), so this never double-fires
    ///     for a boundary already counted, and correctly fires more than once if a single frame's step happens to
    ///     cross more than one boundary.
    /// </summary>
    private void FireSegmentCrossings(float previousAngle, float currentAngle)
    {
        var arc = SegmentArc;
        var previousIndex = (int)Math.Floor(previousAngle / arc);
        var currentIndex = (int)Math.Floor(currentAngle / arc);
        var crossings = currentIndex - previousIndex;

        for (var i = 0; i < crossings; i++)
            SegmentPassed?.Invoke();
    }

    private static Color ResolveLabelColor(byte kind)
        => kind switch
        {
            1 => BonusTextColor,   //Bonus
            2 => JackpotTextColor, //Jackpot
            _ => MultiplierTextColor
        };

    private static SKColor ResolveWedgeColor(byte kind, int index)
        => kind switch
        {
            1 => BonusBand,   //Bonus
            2 => JackpotBand, //Jackpot
            _ => (index % 2 == 0) ? MultiplierBandA : MultiplierBandB
        };

    /// <summary>
    ///     Bakes the wedge-coloured face into <see cref="UIPanel.Background" /> via SkiaSharp -- the same
    ///     static-art technique <c>SlotMachineControl.BuildRecessedPanel</c> uses -- so <see cref="Draw" /> only
    ///     ever rotates one pre-built sprite instead of redrawing every wedge every frame.
    /// </summary>
    private void RebuildFace()
    {
        Background?.Dispose();
        Background = null;

        if (Segments.Length == 0)
            return;

        var info = new SKImageInfo(Diameter, Diameter, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var center = Diameter / 2f;
        var radius = center - RIM_INSET;
        var sweepDegrees = 360f / Segments.Length;
        var rect = new SKRect(center - radius, center - radius, center + radius, center + radius);

        using var wedgePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var rimPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = RimColor
        };

        for (var i = 0; i < Segments.Length; i++)
        {
            //-90 so segment 0 starts at "up" (12 o'clock) rather than Skia's default east (3 o'clock) zero --
            //the same "clockwise from up" convention Draw and SegmentArc's own remarks rely on.
            var startAngle = (i * sweepDegrees) - 90f;
            wedgePaint.Color = ResolveWedgeColor(Segments[i].Kind, i);

            using var wedge = new SKPath();
            wedge.MoveTo(center, center);
            wedge.ArcTo(rect, startAngle, sweepDegrees, false);
            wedge.Close();
            canvas.DrawPath(wedge, wedgePaint);
        }

        //divider lines between wedges, drawn after every fill so they sit crisp on top of every band rather than
        //being painted over by the next wedge's fill.
        for (var i = 0; i < Segments.Length; i++)
        {
            var edgeAngle = ((i * sweepDegrees) - 90f) * (MathF.PI / 180f);
            var edgeX = center + (radius * MathF.Cos(edgeAngle));
            var edgeY = center + (radius * MathF.Sin(edgeAngle));
            canvas.DrawLine(center, center, edgeX, edgeY, rimPaint);
        }

        canvas.DrawCircle(center, center, radius, rimPaint);

        using var snapshot = surface.Snapshot();
        Background = TextureConverter.ToTexture2D(snapshot);
    }
}
