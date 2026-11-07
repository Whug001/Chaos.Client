using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Chaos.DarkAges.TurkeyShoot.TurkeyShootRules;

namespace Chaos.Client.Controls.World.Popups.TurkeyShoot;

/// <summary>
///     The Harvest Turkey Shoot window: a side-view field where flocks cross an autumn sky, Biscuit watches from the
///     wheat, and a click looses a bolt. It draws <see cref="TurkeyShootState" /> and reports shots and leaving. The
///     server judges every shot.
/// </summary>
public sealed class TurkeyShootControl : FramedDialogPanelBase
{
    private const int FIELD_X = 16;
    private const int FIELD_Y = 26;
    private const int PANEL_WIDTH = FIELD_X * 2 + FIELD_WIDTH;
    private const int TITLE_TOP = 8;
    private const int SCORE_TOP = FIELD_Y + FIELD_HEIGHT + 6;
    private const int SCORE_COLUMNS = 3;
    private const int SCORE_WIDTH = FIELD_WIDTH / SCORE_COLUMNS;
    private const int ROW_STEP = TextRenderer.CHAR_HEIGHT + 2;
    private const int FRAME_BOTTOM_BORDER = 47;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int WHEAT_Y = 160;
    private const int BISCUIT_X = 44;
    private const int BISCUIT_Y = 212;
    private const int BOLT_X = 6;
    private const int BOLT_Y = 6;
    private const int BOLT_STEP = 10;
    private const int BANNER_Y = 24;
    private const int SHOT_SOUND = 1;
    private const int HIT_SOUND = 125;
    private const int GOLDEN_HIT_SOUND = 168;
    private const string PLAY_NOTE = "Click the sky to loose a bolt.";

    private static readonly Color HunterColor = LegendColors.White;
    private static readonly Color YouColor = LegendColors.CanaryYellow;
    private static readonly Color LeftColor = new(140, 140, 140);
    private static readonly Color NoteColor = new(214, 190, 140);

    private readonly BiscuitAnimator Biscuit = new(Random.Shared.Next);
    private readonly UILabel NoteLabel;
    private readonly UILabel[] ScoreLabels = new UILabel[MAX_HUNTERS];
    private readonly SoundSystem Sounds;
    private readonly UILabel TitleLabel;

    private float CarryMs;
    private int MouseFieldX = -1;
    private int MouseFieldY = -1;
    private int ResultsHideMs = -1;

    /// <summary>A bolt the local rules allow. The world screen sends it; the server judges it.</summary>
    public event Action<TurkeyShootActionArgs>? ShootRequested;

    /// <summary>The player closed the window. The world screen tells the server the hunter left.</summary>
    public event Action? Closed;

    public TurkeyShootState State { get; } = new();

    public TurkeyShootControl(SoundSystem sounds)
        : base("_nsett", false)
    {
        Sounds = sounds;
        Name = "TurkeyShoot";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;

        var scoreRows = (MAX_HUNTERS + SCORE_COLUMNS - 1) / SCORE_COLUMNS;
        var noteY = SCORE_TOP + scoreRows * ROW_STEP;
        Height = noteY + ROW_STEP + FRAME_BOTTOM_BORDER;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = AddLabel(0, TITLE_TOP, PANEL_WIDTH, HorizontalAlignment.Center, LegendColors.White);

        for (var i = 0; i < MAX_HUNTERS; i++)
            ScoreLabels[i] = AddLabel(
                FIELD_X + i % SCORE_COLUMNS * SCORE_WIDTH,
                SCORE_TOP + i / SCORE_COLUMNS * ROW_STEP,
                SCORE_WIDTH,
                HorizontalAlignment.Left,
                HunterColor);

        NoteLabel = AddLabel(FIELD_X, noteY, FIELD_WIDTH, HorizontalAlignment.Center, NoteColor);
    }

    /// <summary>A round started for this hunter. Clears the last round and shows the window.</summary>
    public void Open(TurkeyShootDisplayArgs args)
    {
        State.Open(args);
        Biscuit.Play(BiscuitAct.Nap);
        ResultsHideMs = -1;
        CarryMs = 0f;
        RefreshLabels();

        base.Show();
    }

    /// <summary>Any round message after Open: flocks, shots, leavers and results.</summary>
    public void Apply(TurkeyShootDisplayArgs args)
    {
        var cue = State.Apply(args);
        Biscuit.Cue(cue);

        switch (cue)
        {
            case TurkeyShootCue.TurkeyHit:
                Sounds.PlaySound(HIT_SOUND);

                break;
            case TurkeyShootCue.GoldenHit:
                Sounds.PlaySound(GOLDEN_HIT_SOUND);

                break;
            case TurkeyShootCue.Results:
                ResultsHideMs = RESULTS_MS;

                break;
        }

        RefreshLabels();
    }

    /// <summary>Hides without telling the server: the server closed the round, or this hunter already left the map.</summary>
    public void CloseFromServer()
    {
        ResultsHideMs = -1;
        base.Hide();
    }

    public override void Hide()
    {
        var wasVisible = Visible;
        ResultsHideMs = -1;

        base.Hide();

        if (wasVisible)
            Closed?.Invoke();
    }

    /// <summary>Ticked by the world screen, in seconds.</summary>
    public void Update(float seconds)
    {
        if (!Visible)
            return;

        //whole milliseconds only, so the flock clock never drifts from the server's
        CarryMs += seconds * 1000f;
        var elapsedMs = (int)CarryMs;

        if (elapsedMs <= 0)
            return;

        CarryMs -= elapsedMs;
        State.Tick(elapsedMs);
        Biscuit.Tick(elapsedMs);

        if (ResultsHideMs < 0)
            return;

        ResultsHideMs -= elapsedMs;

        //the round is over on the server too, so leaving is not news to it
        if (ResultsHideMs < 0)
            CloseFromServer();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawSheet(spriteBatch, "sky", 0, 0, 0, false);

        foreach (var turkey in State.VisibleTurkeys())
            DrawSheet(spriteBatch, turkey.Golden ? "turkey_gold" : "turkey", turkey.Frame, turkey.X, turkey.Y, !turkey.FacesRight);

        DrawSheet(spriteBatch, "wheat", 0, 0, WHEAT_Y, false);
        DrawSheet(spriteBatch, "biscuit", Biscuit.Frame, BISCUIT_X, BISCUIT_Y, false);

        foreach (var (x, y) in State.VisibleMisses())
            DrawSheet(spriteBatch, "puff", 0, x, y, false);

        if (State.Results is not null)
        {
            DrawSheet(spriteBatch, "banner", 0, FIELD_WIDTH / 2, BANNER_Y, false);

            return;
        }

        if (State.FlockIndex >= 0)
            for (var i = 0; i < BOLTS_PER_FLOCK; i++)
                DrawSheet(spriteBatch, "bolt", i < State.BoltsLeft ? 0 : 1, BOLT_X + i * BOLT_STEP, BOLT_Y, false);

        if (TurkeyShootState.InField(MouseFieldX, MouseFieldY))
            DrawSheet(spriteBatch, "crosshair", 0, MouseFieldX, MouseFieldY, false);
    }

    /// <summary>
    ///     Crops a sprite to the field. <paramref name="fieldDest" /> is where the whole frame would land, in field units.
    ///     Returns null when no pixel shows. A flipped sprite shows its source mirrored, so a crop on its left side
    ///     removes source columns from the right.
    /// </summary>
    public static (Rectangle Source, Rectangle Dest)? ClipToField(Rectangle source, Rectangle fieldDest, bool flip)
    {
        var left = Math.Max(0, fieldDest.Left);
        var top = Math.Max(0, fieldDest.Top);
        var right = Math.Min(FIELD_WIDTH, fieldDest.Right);
        var bottom = Math.Min(FIELD_HEIGHT, fieldDest.Bottom);

        if ((right <= left) || (bottom <= top))
            return null;

        var cropLeft = left - fieldDest.Left;
        var cropTop = top - fieldDest.Top;
        var visibleWidth = right - left;
        var visibleHeight = bottom - top;
        var sourceX = flip ? source.X + source.Width - cropLeft - visibleWidth : source.X + cropLeft;

        return (new Rectangle(sourceX, source.Y + cropTop, visibleWidth, visibleHeight), new Rectangle(left, top, visibleWidth, visibleHeight));
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        MouseFieldX = e.ScreenX - ScreenX - FIELD_X;
        MouseFieldY = e.ScreenY - ScreenY - FIELD_Y;
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        var x = e.ScreenX - ScreenX - FIELD_X;
        var y = e.ScreenY - ScreenY - FIELD_Y;

        if (!TurkeyShootState.InField(x, y))
            return;

        e.Handled = true;
        MouseFieldX = x;
        MouseFieldY = y;

        if (State.TryShoot(x, y) is not { } shot)
            return;

        Sounds.PlaySound(SHOT_SOUND);
        ShootRequested?.Invoke(shot);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode != Keycode.Escape)
            return;

        Hide();
        e.Handled = true;
    }

    private UILabel AddLabel(int x, int y, int width, HorizontalAlignment alignment, Color color)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color,
            ShadowStyle = ShadowStyle.BottomRight,
            IsHitTestVisible = false
        };

        AddChild(label);

        return label;
    }

    private void DrawSheet(SpriteBatch spriteBatch, string key, int frame, double fieldX, double fieldY, bool flip)
    {
        var sheet = TurkeyShootArtAssets.ReadManifest()[key];

        //a mirrored frame keeps its anchor pixel on the drawing point
        var anchorX = flip ? sheet.FrameWidth - 1 - sheet.AnchorX : sheet.AnchorX;

        var dest = new Rectangle(
            (int)Math.Round(fieldX) - anchorX,
            (int)Math.Round(fieldY) - sheet.AnchorY,
            sheet.FrameWidth,
            sheet.FrameHeight);

        if (ClipToField(sheet.Frame(frame), dest, flip) is not { } clip)
            return;

        var screen = clip.Dest;
        screen.Offset(ScreenX + FIELD_X, ScreenY + FIELD_Y);

        spriteBatch.Draw(
            TurkeyShootArtAssets.Texture(ChaosGame.Device, key),
            screen,
            clip.Source,
            Color.White,
            0f,
            Vector2.Zero,
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0f);
    }

    private void RefreshLabels()
    {
        TitleLabel.Text = State.Results is null ? State.Title : State.ResultHeadline();
        NoteLabel.Text = State.Results is null ? PLAY_NOTE : State.ResultNote();

        var standings = State.Standings();

        for (var i = 0; i < ScoreLabels.Length; i++)
        {
            var label = ScoreLabels[i];

            if (i >= standings.Count)
            {
                label.Text = string.Empty;

                continue;
            }

            var row = standings[i];
            var left = row.Gone;

            label.Text = State.Results is null ? left ? $"{row.Name} - gone" : $"{row.Name} {row.Points}" : left ? $"{row.Place}. {row.Name} - gone" : $"{row.Place}. {row.Name} {row.Points}";
            label.ForegroundColor = left ? LeftColor : row.Slot == State.YourSlot ? YouColor : HunterColor;
        }
    }
}
