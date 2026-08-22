#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.ViewPort;

/// <summary>
///     Strip showing the local player's class resource, 0-100 - the Plague Doctor's harvested Ichor, the
///     Berserker's Rage or the Assassin's Malice, whichever the server last reported. One strip serves them all
///     because no character carries more than one; they are told apart by fill colour and label (Ichor green,
///     Rage red, Malice violet). A viewport overlay rather than a HUD child so it is built once for both the
///     compact and expanded HUD layouts, mirroring
///     <see cref="SongBarControl" />'s placement mechanism: <c>WorldScreen</c> calls <see cref="SetStripBounds" />
///     every frame with a column derived from whichever HUD layout is active. Hidden whenever
///     <see cref="WorldState.ClassResource" /> has nothing to show. The server is the sole source of truth for the
///     value -- this control only displays what was last received, it never computes the resource itself.
/// </summary>
public sealed class ClassResourceBarControl : UIPanel
{
    public const int STRIP_HEIGHT = 9;

    //only used until WorldScreen's first SetStripBounds call lines the strip up with the active hud
    private const int DEFAULT_WIDTH = 200;

    private const int SIDE_PADDING = 4;
    private const int FILL_INSET = 2;

    private static readonly Color IchorFillColor = new(120, 200, 90, 200);
    private static readonly Color RageFillColor = new(200, 70, 60, 200);

    //violet, deliberately nowhere near the rage red: malice and rage are both kill-fed and both climb, so a
    //player who alts between assassin and berserker must never have to look twice to tell which bar this is.
    private static readonly Color MaliceFillColor = new(160, 90, 210, 200);

    public UIProgressBar Fill { get; }
    public UILabel Label { get; }

    public ClassResourceBarControl()
    {
        Height = STRIP_HEIGHT;
        BackgroundColor = new Color(0, 0, 0, 128);

        Fill = new UIProgressBar
        {
            Name = "ClassResourceFill",
            X = FILL_INSET,
            Y = FILL_INSET,
            Height = STRIP_HEIGHT - (FILL_INSET * 2),
            FillColor = IchorFillColor
        };
        AddChild(Fill);

        Label = new UILabel
        {
            Name = "ClassResourceLabel",
            X = SIDE_PADDING,
            Y = 0,
            Height = STRIP_HEIGHT,
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            PaddingLeft = 0,
            PaddingRight = 0,
            PaddingTop = 0
        };
        AddChild(Label);

        //a placeholder so the fill/label have sane widths before WorldScreen's first SetStripBounds call
        SetStripBounds((ChaosGame.VIRTUAL_WIDTH - DEFAULT_WIDTH) / 2, DEFAULT_WIDTH);
    }

    /// <summary>
    ///     Moves and resizes the strip to span the given screen column, and re-lays out the fill/label inside it.
    ///     Called every frame from <c>WorldScreen.Draw</c>, same as <see cref="SongBarControl.SetStripBounds" />,
    ///     so the strip tracks a HUD layout swap without any extra wiring. Does nothing when the bounds are
    ///     unchanged.
    /// </summary>
    public void SetStripBounds(int x, int width)
    {
        if ((X == x) && (Width == width))
            return;

        X = x;
        Width = width;

        Fill.Width = Math.Max(0, width - (FILL_INSET * 2));
        Label.Width = width;
    }

    /// <summary>
    ///     Refreshes visibility, fill colour/percentage, and label text from <see cref="WorldState.ClassResource" />.
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        var resource = WorldState.ClassResource;

        Visible = resource.HasValue;

        if (!Visible)
            return;

        var (fillColor, resourceName) = resource.Kind switch
        {
            ClassResourceKind.Rage   => (RageFillColor, "Rage"),
            ClassResourceKind.Malice => (MaliceFillColor, "Malice"),
            _                        => (IchorFillColor, "Ichor")
        };

        Fill.FillColor = fillColor;
        Fill.UpdateValue(resource.Amount, 100);
        Label.Text = $"{resourceName} {resource.Amount}/100";

        base.Update(gameTime);
    }
}
